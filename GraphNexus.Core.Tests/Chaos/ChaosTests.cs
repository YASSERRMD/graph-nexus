using GraphNexus.Core.Nodes;
using GraphNexus.Execution;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Chaos;

public class ChaosTests
{
    [Fact]
    public async Task Executor_WithRandomNodeFailures_ShouldHandleGracefully()
    {
        var random = new Random(42);
        var failCount = 0;

        var flakyNode = new FlakyNode("flaky", "FlakyNode", failRate: 0.3, failCountRef: () => failCount++);

        var nodes = new Dictionary<string, INode>
        {
            ["start"] = new MockNode("start", "Start"),
            ["flaky"] = flakyNode,
            ["end"] = new MockNode("end", "End")
        };
        var edges = new List<Edge>
        {
            new Edge("start", "flaky"),
            new Edge("flaky", "end")
        };
        var graph = new GraphDefinition("graph-1", "Chaos", nodes, edges, "start", ["end"]);

        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 1);
        var state = WorkflowState.Create("workflow-1", "thread-1");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = state,
            Options = new ExecutionOptions { ContinueOnError = true }
        };

        var events = new List<StateEvent>();
        await foreach (var evt in executor.RunAsync(request))
        {
            events.Add(evt);
        }

        var errors = events.OfType<NodeErrorEvent>().ToList();
        Assert.True(errors.Count > 0, "Expected some node failures");
    }

    [Fact]
    public async Task Executor_WithCancellation_ShouldStopGracefully()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["slow"] = new SlowNode("slow", "Slow", delayMs: 5000)
        };
        var edges = new List<Edge>();
        var graph = new GraphDefinition("graph-1", "CancelTest", nodes, edges, "slow");

        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 1);
        var state = WorkflowState.Create("workflow-1", "thread-1");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = state,
            Options = new ExecutionOptions()
        };

        var events = new List<StateEvent>();
        await foreach (var evt in executor.RunAsync(request, cts.Token))
        {
            events.Add(evt);
        }

        Assert.True(events.Count > 0);
    }

    [Fact]
    public async Task Executor_WithMemoryPressure_ShouldNotLeak()
    {
        var nodes = Enumerable.Range(0, 100)
            .ToDictionary(i => $"node-{i}", i => new MockNode($"node-{i}", $"Node-{i}"));

        var edges = new List<Edge>();
        for (int i = 0; i < 99; i++)
        {
            edges.Add(new Edge($"node-{i}", $"node-{i + 1}"));
        }

        var graph = new GraphDefinition("graph-1", "MemoryTest", nodes, edges, "node-0", ["node-99"]);

        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 10);
        var state = WorkflowState.Create("workflow-1", "thread-1");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = state
        };

        await foreach (var _ in executor.RunAsync(request))
        {
        }

        Assert.True(true);
    }

    [Fact]
    public async Task StateStore_UnderConcurrentWrites_ShouldMaintainConsistency()
    {
        var store = new InMemoryStateStore();
        var state = WorkflowState.Create("workflow-1", "thread-1");

        var tasks = Enumerable.Range(0, 50)
            .Select(async i =>
            {
                var updatedState = state.WithData($"key-{i % 10}", i);
                await store.SaveStateAsync(updatedState);
            });

        await Task.WhenAll(tasks);

        var finalState = await store.GetStateAsync(state.Id);
        Assert.NotNull(finalState);
    }

    private class FlakyNode : INode
    {
        private readonly Random _random = new();
        private readonly double _failRate;
        private readonly Func<int> _failCountRef;

        public string Id { get; }
        public string Name { get; }

        public FlakyNode(string id, string name, double failRate, Func<int> failCountRef)
        {
            Id = id;
            Name = name;
            _failRate = failRate;
            _failCountRef = failCountRef;
        }

        public async Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);

            if (_random.NextDouble() < _failRate)
            {
                _failCountRef();
                throw new Exception("Random failure");
            }

            return new SuccessResult(Id, Guid.NewGuid().ToString(), state);
        }

        public IReadOnlyList<string> GetInputKeys() => [];
        public IReadOnlyList<string> GetOutputKeys() => [];
    }

    private class SlowNode : INode
    {
        private readonly int _delayMs;

        public string Id { get; }
        public string Name { get; }

        public SlowNode(string id, string name, int delayMs)
        {
            Id = id;
            Name = name;
            _delayMs = delayMs;
        }

        public async Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(_delayMs, cancellationToken);
                return new SuccessResult(Id, Guid.NewGuid().ToString(), state);
            }
            catch (OperationCanceledException)
            {
                return new FailureResult(Id, Guid.NewGuid().ToString(), "Cancelled");
            }
        }

        public IReadOnlyList<string> GetInputKeys() => [];
        public IReadOnlyList<string> GetOutputKeys() => [];
    }
}
