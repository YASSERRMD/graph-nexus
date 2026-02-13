using GraphNexus.Core.Nodes;
using GraphNexus.Core.Tests.Nodes;
using GraphNexus.Execution;
using GraphNexus.Graph;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Execution;

public class ParallelExecutorTests
{
    private GraphDefinition CreateLinearGraph()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = new MockNode("a", "NodeA"),
            ["b"] = new MockNode("b", "NodeB"),
            ["c"] = new MockNode("c", "NodeC")
        };
        var edges = new List<Edge>
        {
            new Edge("a", "b"),
            new Edge("b", "c")
        };
        return new GraphDefinition("graph-1", "Linear", nodes, edges, "a", ["c"]);
    }

    [Fact]
    public async Task RunAsync_LinearGraph_ShouldExecuteAllNodes()
    {
        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 1);
        var graph = CreateLinearGraph();
        var initialState = WorkflowState.Create("workflow-1", "thread-1");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = initialState
        };

        var events = new List<StateEvent>();
        await foreach (var evt in executor.RunAsync(request))
        {
            events.Add(evt);
        }

        var enteredEvents = events.OfType<NodeEnteredEvent>().ToList();
        Assert.Equal(3, enteredEvents.Count);
    }

    [Fact]
    public async Task RunAsync_OnComplete_ShouldEmitCompletedEvent()
    {
        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 1);
        var graph = CreateLinearGraph();
        var initialState = WorkflowState.Create("workflow-1", "thread-1");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = initialState
        };

        StateEvent? finalEvent = null;
        await foreach (var evt in executor.RunAsync(request))
        {
            if (evt is WorkflowCompletedEvent || evt is WorkflowFailedEvent)
            {
                finalEvent = evt;
            }
        }

        Assert.NotNull(finalEvent);
        Assert.IsType<WorkflowCompletedEvent>(finalEvent);
    }

    [Fact]
    public async Task RunAsync_WithFailure_ShouldFailWhenNotContinueOnError()
    {
        var failingNode = new MockNode("b", "FailingNode", (state, ct) =>
            Task.FromResult<NodeResult>(new FailureResult("b", "exec-1", "Intentional failure")));

        var nodes = new Dictionary<string, INode>
        {
            ["a"] = new MockNode("a", "NodeA"),
            ["b"] = failingNode
        };
        var edges = new List<Edge> { new Edge("a", "b") };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a", ["b"]);

        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 1);
        var initialState = WorkflowState.Create("workflow-1", "thread-1");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = initialState,
            Options = new ExecutionOptions { ContinueOnError = false }
        };

        var events = new List<StateEvent>();
        await foreach (var evt in executor.RunAsync(request))
        {
            events.Add(evt);
        }

        var failedEvent = events.OfType<WorkflowFailedEvent>().FirstOrDefault();
        Assert.NotNull(failedEvent);
        Assert.Equal(WorkflowStatus.Failed, failedEvent.State.Status);
    }

    [Fact]
    public async Task RunToCompletionAsync_ShouldReturnFinalState()
    {
        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 1);
        var graph = CreateLinearGraph();
        var initialState = WorkflowState.Create("workflow-1", "thread-1");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = initialState
        };

        var finalState = await executor.RunToCompletionAsync(request);

        Assert.Equal(WorkflowStatus.Completed, finalState.Status);
    }

    [Fact]
    public async Task RunAsync_WithConditionalEdge_ShouldEvaluatePredicate()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = new MockNode("a", "NodeA"),
            ["b"] = new MockNode("b", "NodeB"),
            ["c"] = new MockNode("c", "NodeC")
        };
        var edges = new List<Edge>
        {
            new Edge("a", "b", predicate: EdgePredicates.WhenDataEquals("route", "b")),
            new Edge("a", "c", predicate: EdgePredicates.WhenDataEquals("route", "c"))
        };
        var graph = new GraphDefinition("graph-1", "Conditional", nodes, edges, "a", ["b", "c"]);

        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 1);
        var initialState = WorkflowState.Create("workflow-1", "thread-1").WithData("route", "b");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = initialState
        };

        var events = new List<StateEvent>();
        await foreach (var evt in executor.RunAsync(request))
        {
            events.Add(evt);
        }

        var enteredEvents = events.OfType<NodeEnteredEvent>().ToList();
        Assert.Contains(enteredEvents, e => e.NodeId == "a");
        Assert.Contains(enteredEvents, e => e.NodeId == "b");
        Assert.DoesNotContain(enteredEvents, e => e.NodeId == "c");
    }

    [Fact]
    public async Task RunAsync_ParallelExecution_ShouldRespectConcurrencyLimit()
    {
        var executionOrder = new List<string>();
        var semaphore = new SemaphoreSlim(0);

        var slowNode = new MockNode("slow", "SlowNode", async (state, ct) =>
        {
            executionOrder.Add("slow-start");
            await semaphore.WaitAsync(ct);
            executionOrder.Add("slow-end");
            return new SuccessResult("slow", "exec-1", state);
        });

        var nodes = new Dictionary<string, INode>
        {
            ["a"] = new MockNode("a", "NodeA"),
            ["slow"] = slowNode,
            ["b"] = new MockNode("b", "NodeB", (state, ct) =>
            {
                executionOrder.Add("b-exec");
                return Task.FromResult<NodeResult>(new SuccessResult("b", "exec-1", state));
            })
        };
        var edges = new List<Edge>
        {
            new Edge("a", "slow"),
            new Edge("a", "b")
        };
        var graph = new GraphDefinition("graph-1", "Parallel", nodes, edges, "a", ["slow", "b"]);

        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store, maxConcurrency: 1);
        var initialState = WorkflowState.Create("workflow-1", "thread-1");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = initialState,
            Options = new ExecutionOptions { MaxConcurrency = 1 }
        };

        var task = Task.Run(async () =>
        {
            await foreach (var _ in executor.RunAsync(request))
            {
            }
        });

        await Task.Delay(100);
        Assert.Contains("slow-start", executionOrder);
        Assert.DoesNotContain("b-exec", executionOrder);

        semaphore.Release();
        await task;

        Assert.Contains("b-exec", executionOrder);
    }
}
