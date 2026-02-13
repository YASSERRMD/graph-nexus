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
    public async Task RunAsync_LinearGraph_ShouldExecuteInOrder()
    {
        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store);
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
        Assert.Equal("a", enteredEvents[0].NodeId);
        Assert.Equal("b", enteredEvents[1].NodeId);
        Assert.Equal("c", enteredEvents[2].NodeId);
    }

    [Fact]
    public async Task RunAsync_OnComplete_ShouldEmitCompletedEvent()
    {
        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store);
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
        var executor = new ParallelExecutor(store);
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
    public async Task RunAsync_WithFailure_ShouldContinueWhenContinueOnError()
    {
        var failingNode = new MockNode("b", "FailingNode", (state, ct) =>
            Task.FromResult<NodeResult>(new FailureResult("b", "exec-1", "Intentional failure")));

        var nodes = new Dictionary<string, INode>
        {
            ["a"] = new MockNode("a", "NodeA"),
            ["b"] = failingNode,
            ["c"] = new MockNode("c", "NodeC")
        };
        var edges = new List<Edge>
        {
            new Edge("a", "b"),
            new Edge("b", "c")
        };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a", ["c"]);

        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store);
        var initialState = WorkflowState.Create("workflow-1", "thread-1");

        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = initialState,
            Options = new ExecutionOptions { ContinueOnError = true }
        };

        var events = new List<StateEvent>();
        await foreach (var evt in executor.RunAsync(request))
        {
            events.Add(evt);
        }

        var errorEvents = events.OfType<NodeErrorEvent>().ToList();
        var completedEvent = events.OfType<WorkflowCompletedEvent>().FirstOrDefault();

        Assert.NotEmpty(errorEvents);
        Assert.NotNull(completedEvent);
    }

    [Fact]
    public async Task RunToCompletionAsync_ShouldReturnFinalState()
    {
        var store = new InMemoryStateStore();
        var executor = new ParallelExecutor(store);
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
        var executor = new ParallelExecutor(store);
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
}
