using System.Text;
using GraphNexus.Graph;

namespace GraphNexus.Graph;

public sealed class GraphVisualizer
{
    private readonly GraphDefinition _graph;
    private readonly bool _directed;
    private readonly bool _ranked;

    public GraphVisualizer(GraphDefinition graph, bool directed = true, bool ranked = true)
    {
        _graph = graph;
        _directed = directed;
        _ranked = ranked;
    }

    public string ToDot()
    {
        var sb = new StringBuilder();

        sb.AppendLine($"{(_directed ? "digraph" : "graph")} {_graph.Name.Replace(" ", "_")} {{");
        sb.AppendLine("  graph [");
        sb.AppendLine("    rankdir=LR;");
        sb.AppendLine("    node [shape=box];");
        sb.AppendLine("  ];");

        foreach (var node in _graph.Nodes.Values)
        {
            var nodeLabel = EscapeLabel(node.Name);
            sb.AppendLine($"  {node.Id} [label=\"{nodeLabel}\" id=\"{node.Id}\"];");
        }

        if (_ranked)
        {
            var exitNodes = _graph.ExitNodeIds.ToList();
            if (exitNodes.Any())
            {
                sb.AppendLine("  { rank=sink; " + string.Join("; ", exitNodes) + "; }");
            }

            if (!string.IsNullOrEmpty(_graph.EntryNodeId))
            {
                sb.AppendLine("  { rank=source; " + _graph.EntryNodeId + "; }");
            }
        }

        var edgeSet = new HashSet<string>();
        foreach (var edge in _graph.Edges)
        {
            var edgeKey = $"{edge.SourceNodeId}->{edge.TargetNodeId}";
            if (!edgeSet.Contains(edgeKey))
            {
                edgeSet.Add(edgeKey);
                var label = string.IsNullOrEmpty(edge.Label) ? "" : $" [label=\"{EscapeLabel(edge.Label)}\"]";
                var arrow = _directed ? "->" : "--";
                sb.AppendLine($"  {edge.SourceNodeId} {arrow} {edge.TargetNodeId}{label};");
            }
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    public string ToMermaid()
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");

        foreach (var node in _graph.Nodes.Values)
        {
            var nodeLabel = EscapeLabel(node.Name);
            sb.AppendLine($"  {node.Id}(\"{nodeLabel}\")");
        }

        foreach (var edge in _graph.Edges)
        {
            var label = string.IsNullOrEmpty(edge.Label) ? "" : $"|\"{EscapeLabel(edge.Label)}\"|";
            sb.AppendLine($"  {edge.SourceNodeId} -->{label} {edge.TargetNodeId}");
        }

        if (!string.IsNullOrEmpty(_graph.EntryNodeId))
        {
            sb.AppendLine($"  Start(({_graph.EntryNodeId})");
            sb.AppendLine($"  Start --> {_graph.EntryNodeId}");
        }

        foreach (var exitNode in _graph.ExitNodeIds)
        {
            sb.AppendLine($"  End(({exitNode})");
            sb.AppendLine($"  {exitNode} --> End");
        }

        return sb.ToString();
    }

    private static string EscapeLabel(string label)
    {
        return label.Replace("\"", "'").Replace("\n", " ").Replace("\r", "");
    }

    public static string ToDot(GraphDefinition graph)
    {
        return new GraphVisualizer(graph).ToDot();
    }

    public static string ToMermaid(GraphDefinition graph)
    {
        return new GraphVisualizer(graph).ToMermaid();
    }
}
