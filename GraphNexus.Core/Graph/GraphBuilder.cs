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
    private readonly List<string> _validationErrors = [];

    public GraphBuilder(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Graph ID cannot be empty", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Graph name cannot be empty", nameof(name));

        _id = id;
        _name = name;
    }

    public GraphBuilder Node(INode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_nodes.ContainsKey(node.Id))
        {
            _validationErrors.Add($"Duplicate node ID: {node.Id}");
        }

        _nodes[node.Id] = node;
        return this;
    }

    public GraphBuilder Node(string id, string name, INode node)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(node);

        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Node ID cannot be empty", nameof(id));

        if (_nodes.ContainsKey(id))
        {
            _validationErrors.Add($"Duplicate node ID: {id}");
        }

        _nodes[id] = node;
        return this;
    }

    public GraphBuilder Edge(string sourceId, string targetId, string? label = null, Func<WorkflowState, bool>? predicate = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source node ID cannot be empty", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Target node ID cannot be empty", nameof(targetId));

        _edges.Add(new Edge(sourceId, targetId, label, predicate));
        return this;
    }

    public GraphBuilder Entry(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);

        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("Entry node ID cannot be empty", nameof(nodeId));

        _entryNodeId = nodeId;
        return this;
    }

    public GraphBuilder Exit(string nodeId)
    {
        ArgumentNullException.ThrowIfNull(nodeId);

        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("Exit node ID cannot be empty", nameof(nodeId));

        _exitNodeIds.Add(nodeId);
        return this;
    }

    public GraphBuilder Exits(params string[] nodeIds)
    {
        foreach (var id in nodeIds)
        {
            Exit(id);
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
        if (_nodes.Count == 0)
        {
            throw new InvalidOperationException("Graph must contain at least one node");
        }

        foreach (var edge in _edges)
        {
            if (!_nodes.ContainsKey(edge.SourceNodeId))
            {
                _validationErrors.Add($"Edge references unknown source node: {edge.SourceNodeId}");
            }
            if (!_nodes.ContainsKey(edge.TargetNodeId))
            {
                _validationErrors.Add($"Edge references unknown target node: {edge.TargetNodeId}");
            }
        }

        if (!string.IsNullOrEmpty(_entryNodeId) && !_nodes.ContainsKey(_entryNodeId))
        {
            _validationErrors.Add($"Entry node '{_entryNodeId}' does not exist in the graph");
        }

        foreach (var exitId in _exitNodeIds)
        {
            if (!_nodes.ContainsKey(exitId))
            {
                _validationErrors.Add($"Exit node '{exitId}' does not exist in the graph");
            }
        }

        if (_validationErrors.Count > 0)
        {
            throw new InvalidOperationException($"Graph validation failed: {string.Join("; ", _validationErrors)}");
        }

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
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("Source node ID cannot be empty", nameof(sourceId));

        _builder = builder;
        _sourceId = sourceId;
        _targetIds = targetIds ?? throw new ArgumentNullException(nameof(targetIds));
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
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Target node ID cannot be empty", nameof(targetId));

        _builder = builder;
        _targetId = targetId;
        _sourceIds = sourceIds ?? throw new ArgumentNullException(nameof(sourceIds));
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
