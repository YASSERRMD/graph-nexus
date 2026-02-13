using GraphNexus.Core.Nodes;
using GraphNexus.Core.Tests.Nodes;
using GraphNexus.Graph;
using Xunit;

namespace GraphNexus.Core.Tests.Graph;

public class GraphVisualizerTests
{
    private INode CreateNode(string id, string name) => new MockNode(id, name);

    [Fact]
    public void ToDot_ShouldGenerateValidDotSyntax()
    {
        var builder = new GraphBuilder("graph-1", "TestGraph")
            .Node(CreateNode("a", "Start"))
            .Node(CreateNode("b", "Process"))
            .Node(CreateNode("c", "End"))
            .Edge("a", "b")
            .Edge("b", "c")
            .Entry("a")
            .Exit("c");

        var graph = builder.Build();
        var dot = GraphVisualizer.ToDot(graph);

        Assert.Contains("digraph TestGraph", dot);
        Assert.Contains("a [label=\"Start\"]", dot);
        Assert.Contains("b [label=\"Process\"]", dot);
        Assert.Contains("c [label=\"End\"]", dot);
        Assert.Contains("a -> b", dot);
        Assert.Contains("b -> c", dot);
    }

    [Fact]
    public void ToDot_WithLabels_ShouldIncludeEdgeLabels()
    {
        var builder = new GraphBuilder("graph-1", "LabeledGraph")
            .Node(CreateNode("a", "Start"))
            .Node(CreateNode("b", "End"))
            .Edge("a", "b", "next");

        var graph = builder.Build();
        var dot = GraphVisualizer.ToDot(graph);

        Assert.Contains("a -> b [label=\"next\"]", dot);
    }

    [Fact]
    public void ToDot_WithRanked_ShouldIncludeRankDirectives()
    {
        var builder = new GraphBuilder("graph-1", "RankedGraph")
            .Node(CreateNode("a", "Start"))
            .Node(CreateNode("b", "End"))
            .Edge("a", "b");

        var graph = builder.Build();
        var dot = GraphVisualizer.ToDot(graph);

        Assert.Contains("rank=source", dot);
        Assert.Contains("rank=sink", dot);
    }

    [Fact]
    public void ToMermaid_ShouldGenerateValidMermaidSyntax()
    {
        var builder = new GraphBuilder("graph-1", "TestGraph")
            .Node(CreateNode("start", "Start"))
            .Node(CreateNode("end", "End"))
            .Edge("start", "end");

        var graph = builder.Build();
        var mermaid = GraphVisualizer.ToMermaid(graph);

        Assert.Contains("flowchart TD", mermaid);
        Assert.Contains("start(\"Start\")", mermaid);
        Assert.Contains("end(\"End\")", mermaid);
        Assert.Contains("start --> end", mermaid);
    }

    [Fact]
    public void ToMermaid_WithEdgeLabels_ShouldIncludeLabels()
    {
        var builder = new GraphBuilder("graph-1", "Labeled")
            .Node(CreateNode("a", "A"))
            .Node(CreateNode("b", "B"))
            .Edge("a", "b", "label");

        var graph = builder.Build();
        var mermaid = GraphVisualizer.ToMermaid(graph);

        Assert.Contains("|label|", mermaid);
    }

    [Fact]
    public void ToMermaid_WithEntryExit_ShouldAddStartEndNodes()
    {
        var builder = new GraphBuilder("graph-1", "Test")
            .Node(CreateNode("a", "A"))
            .Node(CreateNode("b", "B"))
            .Edge("a", "b")
            .Entry("a")
            .Exit("b");

        var graph = builder.Build();
        var mermaid = GraphVisualizer.ToMermaid(graph);

        Assert.Contains("Start", mermaid);
        Assert.Contains("End", mermaid);
        Assert.Contains("Start --> a", mermaid);
        Assert.Contains("b --> End", mermaid);
    }

    [Fact]
    public void ToDot_EscapeSpecialCharacters_ShouldHandleQuotes()
    {
        var builder = new GraphBuilder("graph-1", "Test")
            .Node(CreateNode("node", "Node \"quoted\""));

        var graph = builder.Build();
        var dot = GraphVisualizer.ToDot(graph);

        Assert.Contains("label=\"Node 'quoted'\"", dot);
    }

    [Fact]
    public void ToDot_MultipleEdges_ShouldAvoidDuplicates()
    {
        var builder = new GraphBuilder("graph-1", "Test")
            .Node(CreateNode("a", "A"))
            .Node(CreateNode("b", "B"))
            .Edge("a", "b")
            .Edge("a", "b");

        var graph = builder.Build();
        var dot = GraphVisualizer.ToDot(graph);

        var count = dot.Split("a -> b").Length - 1;
        Assert.Equal(1, count);
    }
}
