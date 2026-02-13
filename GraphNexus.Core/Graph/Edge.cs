using GraphNexus.Primitives;

namespace GraphNexus.Graph;

public sealed record Edge
{
    public required string SourceNodeId { get; init; }
    public required string TargetNodeId { get; init; }
    public string? Label { get; init; }
    public Func<WorkflowState, bool>? Predicate { get; init; }

    public Edge(string sourceNodeId, string targetNodeId, string? label = null, Func<WorkflowState, bool>? predicate = null)
    {
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
        Label = label;
        Predicate = predicate;
    }

    public bool EvaluateCondition(WorkflowState state)
    {
        return Predicate?.Invoke(state) ?? true;
    }
}

public static class EdgePredicates
{
    public static Func<WorkflowState, bool> Always => _ => true;

    public static Func<WorkflowState, bool> WhenDataEquals(string key, object value)
    {
        return state => state.Data.TryGetValue(key, out var v) && Equals(v, value);
    }

    public static Func<WorkflowState, bool> WhenDataContainsKey(string key)
    {
        return state => state.Data.ContainsKey(key);
    }

    public static Func<WorkflowState, bool> WhenStepGreaterThan(int step)
    {
        return state => state.Step > step;
    }

    public static Func<WorkflowState, bool> WhenStepLessThan(int step)
    {
        return state => state.Step < step;
    }

    public static Func<WorkflowState, bool> WhenMessageCountGreaterThan(int count)
    {
        return state => state.Messages.Count > count;
    }

    public static Func<WorkflowState, bool> WhenStatusIs(WorkflowStatus status)
    {
        return state => state.Status == status;
    }

    public static Func<WorkflowState, bool> Custom(Func<WorkflowState, bool> predicate)
    {
        return predicate;
    }
}
