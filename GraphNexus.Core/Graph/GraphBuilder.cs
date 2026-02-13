using System.Collections.Frozen;
using GraphNexus.Graph;
using GraphNexus.Nodes;
using GraphNexus.Primitives;

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

    public GraphBuilder Edge(string sourceId, string targetId, string? label = null, Func<WorkflowState, bool>? predicate = null)
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

    public ForkBuilder Fork(string sourceId, params string[] targetIds)
    {
        return new ForkBuilder(this, sourceId, targetIds);
    }

    public JoinBuilder Join(string targetId, params string[] sourceIds)
    {
        return new JoinBuilder(this, targetId, sourceIds);
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

public class ForkBuilder
{
    private readonly GraphBuilder _builder;
    private readonly string _sourceId;
    private readonly string[] _targetIds;

    public ForkBuilder(GraphBuilder builder, string sourceId, string[] targetIds)
    {
        _builder = builder;
        _sourceId = sourceId;
        _targetIds = targetIds;
    }

    public GraphBuilder WithLabels(params string[] labels)
    {
        for (int i = 0; i < _targetIds.Length; i++)
        {
            var label = i < labels.Length ? labels[i] : null;
            _builder.Edge(_sourceId, _targetIds[i], label);
        }
        return _builder;
    }

    public GraphBuilder WithConditions(params Func<WorkflowState, bool>[] predicates)
    {
        for (int i = 0; i < _targetIds.Length; i++)
        {
            var predicate = i < predicates.Length ? predicates[i] : null;
            _builder.Edge(_sourceId, _targetIds[i], predicate: predicate);
        }
        return _builder;
    }

    public GraphBuilder WithConditionsAndLabels((string Label, Func<WorkflowState, bool> Predicate)[] edges)
    {
        foreach (var (label, predicate) in edges)
        {
            _builder.Edge(_sourceId, label, predicate: predicate);
        }
        return _builder;
    }
}

public class JoinBuilder
{
    private readonly GraphBuilder _builder;
    private readonly string _targetId;
    private readonly string[] _sourceIds;

    public JoinBuilder(GraphBuilder builder, string targetId, string[] sourceIds)
    {
        _builder = builder;
        _targetId = targetId;
        _sourceIds = sourceIds;
    }

    public GraphBuilder WithLabel(string? label = null)
    {
        foreach (var sourceId in _sourceIds)
        {
            _builder.Edge(sourceId, _targetId, label);
        }
        return _builder;
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

    public static GraphBuilder Parallel(
        this GraphBuilder builder,
        string sourceId,
        IReadOnlyList<string> parallelNodeIds,
        string joinNodeId)
    {
        foreach (var nodeId in parallelNodeIds)
        {
            builder.Edge(sourceId, nodeId);
            builder.Edge(nodeId, joinNodeId);
        }
        return builder;
    }
}
