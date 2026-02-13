using System.Collections.Frozen;
using GraphNexus.Graph;
using GraphNexus.Nodes;

namespace GraphNexus.Graph;

public class GraphBuilder
{
    private readonly string _id;
    private readonly string _name;
    private readonly Dictionary<string, INode> _nodes = [];
    private readonly List<Edge> _edges = [];
    private string? _entryNodeId;
    private readonly HashSet<string> _exitNodeIds = [];

    public GraphBuilder(string id, string name)
    {
        _id = id;
        _name = name;
    }

    public GraphBuilder Node(INode node)
    {
        _nodes[node.Id] = node;
        return this;
    }

    public GraphBuilder Node(string id, string name, INode node)
    {
        _nodes[id] = node;
        return this;
    }

    public GraphBuilder Edge(string sourceId, string targetId, string? label = null, Func<Primitives.WorkflowState, bool>? predicate = null)
    {
        _edges.Add(new Edge(sourceId, targetId, label, predicate));
        return this;
    }

    public GraphBuilder Entry(string nodeId)
    {
        _entryNodeId = nodeId;
        return this;
    }

    public GraphBuilder Exit(string nodeId)
    {
        _exitNodeIds.Add(nodeId);
        return this;
    }

    public GraphBuilder Exits(params string[] nodeIds)
    {
        foreach (var id in nodeIds)
        {
            _exitNodeIds.Add(id);
        }
        return this;
    }

    public GraphDefinition Build()
    {
        return new GraphDefinition(
            _id,
            _name,
            _nodes.ToFrozenDictionary(),
            _edges.ToFrozenList(),
            _entryNodeId,
            _exitNodeIds.ToFrozenSet()
        );
    }
}

public static class GraphBuilderExtensions
{
    public static GraphBuilder AddNodes(this GraphBuilder builder, params INode[] nodes)
    {
        foreach (var node in nodes)
        {
            builder.Node(node);
        }
        return builder;
    }

    public static GraphBuilder Linear(this GraphBuilder builder, params string[] nodeIds)
    {
        for (int i = 0; i < nodeIds.Length - 1; i++)
        {
            builder.Edge(nodeIds[i], nodeIds[i + 1]);
        }
        return builder;
    }

    public static GraphBuilder From(this string sourceId, GraphBuilder builder, string targetId, string? label = null)
    {
        builder.Edge(sourceId, targetId, label);
        return builder;
    }

    public static GraphBuilder To(this string targetId, GraphBuilder builder, string sourceId)
    {
        builder.Edge(sourceId, targetId);
        return builder;
    }
}
