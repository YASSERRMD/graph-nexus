using GraphNexus.Core.Nodes;
using GraphNexus.Core.Tests.Nodes;
using GraphNexus.Graph;
using Xunit;

namespace GraphNexus.Core.Tests.Graph;

public class GraphBuilderTests
{
    private INode CreateNode(string id) => new MockNode(id, $"Node-{id}");

    [Fact]
    public void GraphBuilder_Build_ShouldCreateGraphDefinition()
    {
        var builder = new GraphBuilder("graph-1", "TestGraph")
            .Node(CreateNode("a"))
            .Node(CreateNode("b"))
            .Edge("a", "b")
            .Entry("a")
            .Exit("b");

        var graph = builder.Build();

        Assert.Equal("graph-1", graph.Id);
        Assert.Equal("TestGraph", graph.Name);
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);
    }

    [Fact]
    public void GraphBuilder_Build_DefaultEntry_ShouldBeFirstNode()
    {
        var builder = new GraphBuilder("graph-1", "Test")
            .Node(CreateNode("a"))
            .Node(CreateNode("b"))
            .Edge("a", "b");

        var graph = builder.Build();

        Assert.Equal("a", graph.EntryNodeId);
    }

    [Fact]
    public void GraphBuilder_Linear_ShouldCreateEdges()
    {
        var builder = new GraphBuilder("graph-1", "Linear")
            .Node(CreateNode("a"))
            .Node(CreateNode("b"))
            .Node(CreateNode("c"))
            .Linear("a", "b", "c");

        var graph = builder.Build();

        Assert.Equal(2, graph.Edges.Count);
    }

    [Fact]
    public void GraphBuilder_AddNodes_ShouldAddMultipleNodes()
    {
        var builder = new GraphBuilder("graph-1", "Test")
            .AddNodes(CreateNode("a"), CreateNode("b"), CreateNode("c"));

        var graph = builder.Build();

        Assert.Equal(3, graph.Nodes.Count);
    }

    [Fact]
    public void GraphBuilder_Exit_ShouldSetMultipleExits()
    {
        var builder = new GraphBuilder("graph-1", "Test")
            .Node(CreateNode("a"))
            .Node(CreateNode("b"))
            .Node(CreateNode("c"))
            .Edge("a", "b")
            .Edge("a", "c")
            .Exits("b", "c");

        var graph = builder.Build();

        Assert.Contains("b", graph.ExitNodeIds);
        Assert.Contains("c", graph.ExitNodeIds);
    }

    [Fact]
    public void GraphBuilder_WithPredicate_ShouldIncludePredicate()
    {
        var predicateCalled = false;
        Func<Primitives.WorkflowState, bool> predicate = s =>
        {
            predicateCalled = true;
            return true;
        };

        var builder = new GraphBuilder("graph-1", "Test")
            .Node(CreateNode("a"))
            .Node(CreateNode("b"))
            .Edge("a", "b", "conditional", predicate);

        var graph = builder.Build();
        var edge = graph.Edges.First();

        Assert.Equal("conditional", edge.Label);
        Assert.True(predicateCalled);
    }

    [Fact]
    public void GraphBuilder_Build_ShouldAllowDuplicateNodeCalls()
    {
        var nodeA = CreateNode("a");
        var builder = new GraphBuilder("graph-1", "Test")
            .Node(nodeA)
            .Node(CreateNode("b"))
            .Node(CreateNode("c"))
            .Edge("a", "b")
            .Edge("b", "c");

        var graph = builder.Build();

        Assert.Equal(3, graph.Nodes.Count);
    }
}
