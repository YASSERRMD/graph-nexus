using GraphNexus.Core.Nodes;
using GraphNexus.Core.Tests.Nodes;
using GraphNexus.Graph;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Graph;

public class GraphDefinitionTests
{
    private INode CreateNode(string id) => new MockNode(id, $"Node-{id}");

    [Fact]
    public void GraphDefinition_Create_ShouldInitializeCorrectly()
    {
        var nodeA = CreateNode("a");
        var nodeB = CreateNode("b");
        var nodes = new Dictionary<string, INode> { ["a"] = nodeA, ["b"] = nodeB };
        var edges = new List<Edge> { new Edge("a", "b") };

        var graph = new GraphDefinition("graph-1", "TestGraph", nodes, edges, "a", ["b"]);

        Assert.Equal("graph-1", graph.Id);
        Assert.Equal("TestGraph", graph.Name);
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Single(graph.Edges);
        Assert.Equal("a", graph.EntryNodeId);
        Assert.Contains("b", graph.ExitNodeIds);
    }

    [Fact]
    public void GraphDefinition_GetOutgoingEdges_ShouldReturnEdges()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = CreateNode("a"),
            ["b"] = CreateNode("b"),
            ["c"] = CreateNode("c")
        };
        var edges = new List<Edge>
        {
            new Edge("a", "b"),
            new Edge("a", "c")
        };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a");

        var outgoing = graph.GetOutgoingEdges("a");

        Assert.Equal(2, outgoing.Count);
        Assert.Contains(outgoing, e => e.TargetNodeId == "b");
        Assert.Contains(outgoing, e => e.TargetNodeId == "c");
    }

    [Fact]
    public void GraphDefinition_GetIncomingEdges_ShouldReturnEdges()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = CreateNode("a"),
            ["b"] = CreateNode("b"),
            ["c"] = CreateNode("c")
        };
        var edges = new List<Edge>
        {
            new Edge("a", "c"),
            new Edge("b", "c")
        };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a");

        var incoming = graph.GetIncomingEdges("c");

        Assert.Equal(2, incoming.Count);
    }

    [Fact]
    public void GraphDefinition_GetReachableNodes_ShouldReturnAllReachable()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = CreateNode("a"),
            ["b"] = CreateNode("b"),
            ["c"] = CreateNode("c"),
            ["d"] = CreateNode("d")
        };
        var edges = new List<Edge>
        {
            new Edge("a", "b"),
            new Edge("b", "c"),
            new Edge("c", "d")
        };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a");

        var reachable = graph.GetReachableNodes("a");

        Assert.Equal(4, reachable.Count);
        Assert.Contains("a", reachable);
        Assert.Contains("d", reachable);
    }

    [Fact]
    public void GraphDefinition_Validate_ValidGraph_ShouldReturnEmpty()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = CreateNode("a"),
            ["b"] = CreateNode("b")
        };
        var edges = new List<Edge> { new Edge("a", "b") };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a", ["b"]);

        var errors = graph.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void GraphDefinition_Validate_EmptyNodes_ShouldReturnError()
    {
        var nodes = new Dictionary<string, INode>();
        var edges = new List<Edge>();
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges);

        var errors = graph.Validate();

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("at least one node"));
    }

    [Fact]
    public void GraphDefinition_Validate_InvalidEdgeSource_ShouldReturnError()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = CreateNode("a"),
            ["b"] = CreateNode("b")
        };
        var edges = new List<Edge> { new Edge("unknown", "b") };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a");

        var errors = graph.Validate();

        Assert.Contains(errors, e => e.Contains("unknown source node"));
    }

    [Fact]
    public void GraphDefinition_Validate_UnreachableNode_ShouldReturnError()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = CreateNode("a"),
            ["b"] = CreateNode("b"),
            ["c"] = CreateNode("c")
        };
        var edges = new List<Edge> { new Edge("a", "b") };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a");

        var errors = graph.Validate();

        Assert.Contains(errors, e => e.Contains("Unreachable"));
    }

    [Fact]
    public void GraphDefinition_Validate_Cycle_ShouldReturnError()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = CreateNode("a"),
            ["b"] = CreateNode("b"),
            ["c"] = CreateNode("c")
        };
        var edges = new List<Edge>
        {
            new Edge("a", "b"),
            new Edge("b", "c"),
            new Edge("c", "a")
        };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a");

        var errors = graph.Validate();

        Assert.Contains(errors, e => e.Contains("Cycles detected"));
    }

    [Fact]
    public void GraphDefinition_Validate_ValidCycleWithBreak_ShouldPass()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["a"] = CreateNode("a"),
            ["b"] = CreateNode("b"),
            ["c"] = CreateNode("c")
        };
        var edges = new List<Edge>
        {
            new Edge("a", "b"),
            new Edge("b", "c"),
            new Edge("c", "b", predicate: _ => false)
        };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges, "a");

        var errors = graph.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void GraphDefinition_DefaultEntryNode_ShouldBeFirstNode()
    {
        var nodes = new Dictionary<string, INode>
        {
            ["first"] = CreateNode("first"),
            ["second"] = CreateNode("second")
        };
        var edges = new List<Edge> { new Edge("first", "second") };
        var graph = new GraphDefinition("graph-1", "Test", nodes, edges);

        Assert.Equal("first", graph.EntryNodeId);
    }
}
