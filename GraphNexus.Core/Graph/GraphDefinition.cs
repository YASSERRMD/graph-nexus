using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using GraphNexus.Graph;
using GraphNexus.Nodes;

namespace GraphNexus.Graph;

public sealed class GraphDefinition
{
    private readonly Lazy<IReadOnlyList<string>> _validationResult;

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, INode> Nodes { get; }
    public IReadOnlyList<Edge> Edges { get; }
    public string? EntryNodeId { get; }
    public IReadOnlyList<string> ExitNodeIds { get; }

    public GraphDefinition(
        string id,
        string name,
        IReadOnlyDictionary<string, INode> nodes,
        IReadOnlyList<Edge> edges,
        string? entryNodeId = null,
        IReadOnlyList<string>? exitNodeIds = null)
    {
        Id = id;
        Name = name;
        Nodes = nodes;
        Edges = edges;
        EntryNodeId = entryNodeId ?? nodes.Keys.FirstOrDefault();
        ExitNodeIds = exitNodeIds ?? nodes.Keys.Where(k => !edges.Any(e => e.SourceNodeId == k)).ToList();

        _validationResult = new Lazy<IReadOnlyList<string>>(ComputeValidation);
    }

    public IReadOnlyList<Edge> GetOutgoingEdges(string nodeId)
    {
        return Edges.Where(e => e.SourceNodeId == nodeId).ToList();
    }

    public IReadOnlyList<Edge> GetIncomingEdges(string nodeId)
    {
        return Edges.Where(e => e.TargetNodeId == nodeId).ToList();
    }

    public IEnumerable<string> GetReachableNodes(string fromNodeId)
    {
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(fromNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Contains(current))
            {
                visited.Add(current);
                foreach (var edge in GetOutgoingEdges(current))
                {
                    if (!visited.Contains(edge.TargetNodeId))
                    {
                        queue.Enqueue(edge.TargetNodeId);
                    }
                }
            }
        }

        return visited;
    }

    public IReadOnlyList<string> Validate() => _validationResult.Value;

    private IReadOnlyList<string> ComputeValidation()
    {
        var errors = new List<string>();

        if (Nodes.Count == 0)
        {
            errors.Add("Graph must contain at least one node");
        }

        if (string.IsNullOrEmpty(EntryNodeId) || !Nodes.ContainsKey(EntryNodeId))
        {
            errors.Add("Graph must have a valid entry node");
        }

        var nodeIds = Nodes.Keys.ToHashSet();
        foreach (var edge in Edges)
        {
            if (!nodeIds.Contains(edge.SourceNodeId))
            {
                errors.Add($"Edge references unknown source node: {edge.SourceNodeId}");
            }
            if (!nodeIds.Contains(edge.TargetNodeId))
            {
                errors.Add($"Edge references unknown target node: {edge.TargetNodeId}");
            }
        }

        if (!string.IsNullOrEmpty(EntryNodeId))
        {
            var reachable = GetReachableNodes(EntryNodeId);
            var unreachable = nodeIds.Except(reachable);
            if (unreachable.Any())
            {
                errors.Add($"Unreachable nodes found: {string.Join(", ", unreachable)}");
            }
        }

        var cycles = DetectCycles();
        if (cycles.Any())
        {
            errors.Add($"Cycles detected: {string.Join(" -> ", cycles.Select(c => string.Join(" -> ", c)))}");
        }

        return errors;
    }

    private List<List<string>> DetectCycles()
    {
        var cycles = new List<List<string>>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        void DFS(string node)
        {
            visited.Add(node);
            recursionStack.Add(node);
            path.Add(node);

            foreach (var edge in GetOutgoingEdges(node))
            {
                var target = edge.TargetNodeId;
                if (!visited.Contains(target))
                {
                    DFS(target);
                }
                else if (recursionStack.Contains(target))
                {
                    var cycleStart = path.IndexOf(target);
                    cycles.Add(path.Skip(cycleStart).Append(target).ToList());
                }
            }

            path.RemoveAt(path.Count - 1);
            recursionStack.Remove(node);
        }

        foreach (var node in Nodes.Keys)
        {
            if (!visited.Contains(node))
            {
                DFS(node);
            }
        }

        return cycles;
    }
}

public static class GraphDefinitionSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
