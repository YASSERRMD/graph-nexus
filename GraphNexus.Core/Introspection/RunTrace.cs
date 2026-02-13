using GraphNexus.Primitives;

namespace GraphNexus.Introspection;

public sealed class RunTrace
{
    public string ExecutionId { get; init; } = "";
    public string WorkflowId { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public IReadOnlyList<StateEvent> Events { get; init; } = [];
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();

    public TimeSpan Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : DateTimeOffset.UtcNow - StartedAt;

    public bool IsCompleted => Events.Any(e => e is WorkflowCompletedEvent);

    public IReadOnlyList<NodeExecutionInfo> GetNodeExecutions()
    {
        var executions = new List<NodeExecutionInfo>();
        StateEvent? lastEntered = null;

        foreach (var evt in Events)
        {
            if (evt is NodeEnteredEvent entered)
            {
                lastEntered = entered;
            }
            else if (evt is NodeExitedEvent exited && lastEntered != null && lastEntered.NodeId == exited.NodeId)
            {
                executions.Add(new NodeExecutionInfo
                {
                    NodeId = exited.NodeId,
                    EnteredAt = entered.Timestamp,
                    ExitedAt = exited.Timestamp,
                    Duration = exited.Timestamp - entered.Timestamp,
                    State = exited.State
                });
                lastEntered = null;
            }
        }

        return executions;
    }

    public IReadOnlyList<NodeErrorInfo> GetErrors()
    {
        return Events.OfType<NodeErrorEvent>().Select(e => new NodeErrorInfo
        {
            NodeId = e.NodeId,
            Error = e.Error,
            StackTrace = e.StackTrace,
            Timestamp = e.Timestamp,
            State = e.State
        }).ToList();
    }

    public IReadOnlyList<StateEvent> GetEventsByNode(string nodeId)
    {
        return Events.Where(e => e.NodeId == nodeId).ToList();
    }

    public IReadOnlyList<StateEvent> GetEventsByType(StateEventType eventType)
    {
        return Events.Where(e => e.EventType == eventType).ToList();
    }

    public IReadOnlyList<StateEvent> GetEventsInTimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        return Events.Where(e => e.Timestamp >= start && e.Timestamp <= end).ToList();
    }
}

public sealed record NodeExecutionInfo
{
    public required string NodeId { get; init; }
    public required DateTimeOffset EnteredAt { get; init; }
    public required DateTimeOffset ExitedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public required WorkflowState State { get; init; }
}

public sealed record NodeErrorInfo
{
    public required string NodeId { get; init; }
    public required string Error { get; init; }
    public required string StackTrace { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required WorkflowState State { get; init; }
}

public sealed class RunTraceAnalyzer
{
    private readonly RunTrace _trace;

    public RunTraceAnalyzer(RunTrace trace)
    {
        _trace = trace;
    }

    public RunStatistics GetStatistics()
    {
        var nodeExecutions = _trace.GetNodeExecutions();
        var errors = _trace.GetErrors();

        return new RunStatistics
        {
            TotalNodesExecuted = nodeExecutions.Count,
            TotalErrors = errors.Count,
            TotalDuration = _trace.Duration,
            AverageNodeDuration = nodeExecutions.Count > 0
                ? TimeSpan.FromTicks(nodeExecutions.Sum(e => e.Duration.Ticks) / nodeExecutions.Count)
                : TimeSpan.Zero,
            LongestNodeExecution = nodeExecutions.Count > 0
                ? nodeExecutions.MaxBy(e => e.Duration)
                : null,
            ShortestNodeExecution = nodeExecutions.Count > 0
                ? nodeExecutions.MinBy(e => e.Duration)
                : null
        };
    }

    public IReadOnlyList<string> GetExecutionPath()
    {
        return _trace.Events
            .OfType<NodeEnteredEvent>()
            .Select(e => e.NodeId)
            .ToList();
    }

    public IReadOnlyDictionary<string, int> GetNodeExecutionCounts()
    {
        return _trace.Events
            .OfType<NodeEnteredEvent>()
            .GroupBy(e => e.NodeId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public bool HasErrors() => _trace.GetErrors().Count > 0;

    public bool IsHealthy() => HasErrors() == false && _trace.IsCompleted;
}

public sealed record RunStatistics
{
    public int TotalNodesExecuted { get; init; }
    public int TotalErrors { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public TimeSpan AverageNodeDuration { get; init; }
    public NodeExecutionInfo? LongestNodeExecution { get; init; }
    public NodeExecutionInfo? ShortestNodeExecution { get; init; }
}
