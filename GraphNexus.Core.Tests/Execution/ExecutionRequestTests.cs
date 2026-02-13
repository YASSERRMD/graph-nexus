using GraphNexus.Core.Nodes;
using GraphNexus.Core.Tests.Nodes;
using GraphNexus.Execution;
using GraphNexus.Graph;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Execution;

public class ExecutionRequestTests
{
    private GraphDefinition CreateSimpleGraph()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = new MockNode("a", "NodeA"),
            ["b"] = new MockNode("b", "NodeB")
        };
        var edges = new List<Edge> { new Edge("a", "b") };
        return new GraphDefinition("graph-1", "Test", nodes, edges, "a", ["b"]);
    }

    [Fact]
    public void ExecutionRequest_Create_ShouldInitializeCorrectly()
    {
        var graph = CreateSimpleGraph();
        var state = WorkflowState.Create("workflow-1", "thread-1");
        var request = new ExecutionRequest
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            ThreadId = "thread-1",
            Graph = graph,
            InitialState = state,
            Options = new ExecutionOptions { MaxConcurrency = 4 }
        };

        Assert.Equal("exec-1", request.ExecutionId);
        Assert.Equal("workflow-1", request.WorkflowId);
        Assert.Equal("thread-1", request.ThreadId);
        Assert.Equal(4, request.Options.MaxConcurrency);
    }

    [Fact]
    public void ExecutionOptions_DefaultValues_ShouldBeCorrect()
    {
        var options = new ExecutionOptions();

        Assert.Equal(4, options.MaxConcurrency);
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Timeout);
        Assert.False(options.ContinueOnError);
    }

    [Fact]
    public void NodeExecutionRequest_Create_ShouldInitializeCorrectly()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1");
        var request = new NodeExecutionRequest
        {
            ExecutionId = "exec-1",
            NodeId = "node-1",
            State = state,
            RetryCount = 0,
            CancellationToken = CancellationToken.None
        };

        Assert.Equal("exec-1", request.ExecutionId);
        Assert.Equal("node-1", request.NodeId);
        Assert.Equal(0, request.RetryCount);
    }

    [Fact]
    public void NodeExecutionResult_Create_ShouldInitializeCorrectly()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1");
        var result = new NodeExecutionResult
        {
            NodeId = "node-1",
            State = state,
            Success = true
        };

        Assert.Equal("node-1", result.NodeId);
        Assert.True(result.Success);
    }

    [Fact]
    public void ExecutionContext_Create_ShouldInitializeChannels()
    {
        var graph = CreateSimpleGraph();
        var store = new InMemoryStateStore();

        var context = new ExecutionContext(
            "exec-1",
            "workflow-1",
            "thread-1",
            graph,
            store,
            new ExecutionOptions(),
            CancellationToken.None
        );

        Assert.Equal("exec-1", context.ExecutionId);
        Assert.True(context.ExecutionReader.CanRead);
        Assert.True(context.ResultReader.CanRead);
    }

    [Fact]
    public void ExecutionContext_AddEvent_ShouldStoreEvent()
    {
        var graph = CreateSimpleGraph();
        var store = new InMemoryStateStore();
        var state = WorkflowState.Create("workflow-1", "thread-1");
        var evt = new NodeEnteredEvent("evt-1", "exec-1", "node-1", state);

        var context = new ExecutionContext(
            "exec-1",
            "workflow-1",
            "thread-1",
            graph,
            store,
            new ExecutionOptions(),
            CancellationToken.None
        );
        context.AddEvent(evt);

        var events = context.GetEvents();
        Assert.Single(events);
        Assert.Equal("evt-1", events[0].Id);
    }
}
