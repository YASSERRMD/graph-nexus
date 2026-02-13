using GraphNexus.Core.Nodes;
using GraphNexus.Core.Tests.Nodes;
using GraphNexus.Graph;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Graph;

public class ForkJoinTests
{
    private INode CreateNode(string id) => new MockNode(id, $"Node-{id}");

    [Fact]
    public void GraphBuilder_Fork_ShouldCreateMultipleEdges()
    {
        var builder = new GraphBuilder("graph-1", "ForkTest")
            .Node(CreateNode("start"))
            .Node(CreateNode("branch-a"))
            .Node(CreateNode("branch-b"))
            .Node(CreateNode("join"));

        builder.Fork("start", "branch-a", "branch-b");

        var graph = builder.Build();

        var outgoingFromStart = graph.GetOutgoingEdges("start");
        Assert.Equal(2, outgoingFromStart.Count);
    }

    [Fact]
    public void GraphBuilder_Fork_WithLabels_ShouldAssignLabels()
    {
        var builder = new GraphBuilder("graph-1", "ForkTest")
            .Node(CreateNode("start"))
            .Node(CreateNode("a"))
            .Node(CreateNode("b"));

        builder.Fork("start", "a", "b").WithLabels("path-a", "path-b");

        var graph = builder.Build();
        var edges = graph.Edges.ToList();

        Assert.Equal("path-a", edges[0].Label);
        Assert.Equal("path-b", edges[1].Label);
    }

    [Fact]
    public void GraphBuilder_Fork_WithConditions_ShouldAssignPredicates()
    {
        var builder = new GraphBuilder("graph-1", "ForkTest")
            .Node(CreateNode("start"))
            .Node(CreateNode("a"))
            .Node(CreateNode("b"));

        var predA = EdgePredicates.WhenDataEquals("type", "a");
        var predB = EdgePredicates.WhenDataEquals("type", "b");
        builder.Fork("start", "a", "b").WithConditions(predA, predB);

        var graph = builder.Build();

        var stateA = WorkflowState.Create("w1").WithData("type", "a");
        var stateB = WorkflowState.Create("w1").WithData("type", "b");

        var edgeToA = graph.Edges.First(e => e.TargetNodeId == "a");
        var edgeToB = graph.Edges.First(e => e.TargetNodeId == "b");

        Assert.True(edgeToA.EvaluateCondition(stateA));
        Assert.False(edgeToA.EvaluateCondition(stateB));
        Assert.False(edgeToB.EvaluateCondition(stateA));
        Assert.True(edgeToB.EvaluateCondition(stateB));
    }

    [Fact]
    public void GraphBuilder_Join_ShouldCreateMultipleIncomingEdges()
    {
        var builder = new GraphBuilder("graph-1", "JoinTest")
            .Node(CreateNode("start"))
            .Node(CreateNode("a"))
            .Node(CreateNode("b"))
            .Node(CreateNode("end"))
            .Edge("start", "a")
            .Edge("start", "b");

        builder.Join("end", "a", "b");

        var graph = builder.Build();

        var incomingToEnd = graph.GetIncomingEdges("end");
        Assert.Equal(2, incomingToEnd.Count);
    }

    [Fact]
    public void GraphBuilder_Join_WithLabel_ShouldAssignLabel()
    {
        var builder = new GraphBuilder("graph-1", "JoinTest")
            .Node(CreateNode("a"))
            .Node(CreateNode("b"))
            .Node(CreateNode("end"));

        builder.Join("end", "a", "b").WithLabel("merged");

        var graph = builder.Build();

        foreach (var edge in graph.Edges)
        {
            Assert.Equal("merged", edge.Label);
        }
    }

    [Fact]
    public void GraphBuilder_Parallel_ShouldSetupForkJoin()
    {
        var builder = new GraphBuilder("graph-1", "Parallel")
            .Node(CreateNode("start"))
            .Node(CreateNode("tool-a"))
            .Node(CreateNode("tool-b"))
            .Node(CreateNode("join"));

        builder.Parallel("start", ["tool-a", "tool-b"], "join");

        var graph = builder.Build();

        var outgoingFromStart = graph.GetOutgoingEdges("start");
        var incomingToJoin = graph.GetIncomingEdges("join");

        Assert.Equal(2, outgoingFromStart.Count);
        Assert.Equal(2, incomingToJoin.Count);
    }

    [Fact]
    public void GraphBuilder_ComplexForkJoin_ShouldBuildValidGraph()
    {
        var builder = new GraphBuilder("graph-1", "Complex")
            .Node(CreateNode("start"))
            .Node(CreateNode("process-a"))
            .Node(CreateNode("process-b"))
            .Node(CreateNode("validate"))
            .Node(CreateNode("end"))
            .Entry("start")
            .Exit("end");

        builder
            .Fork("start", "process-a", "process-b")
                .WithConditions(
                    EdgePredicates.WhenDataEquals("route", "a"),
                    EdgePredicates.WhenDataEquals("route", "b"))
            .Join("validate", "process-a", "process-b")
            .Edge("validate", "end");

        var graph = builder.Build();
        var errors = graph.Validate();

        Assert.Empty(errors);
    }
}
