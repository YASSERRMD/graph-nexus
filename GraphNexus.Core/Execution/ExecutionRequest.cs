using GraphNexus.Graph;
using GraphNexus.Primitives;

namespace GraphNexus.Execution;

public sealed record ExecutionRequest
{
    public required string ExecutionId { get; init; }
    public required string WorkflowId { get; init; }
    public required string ThreadId { get; init; }
    public required GraphDefinition Graph { get; init; }
    public required WorkflowState InitialState { get; init; }
    public ExecutionOptions Options { get; init; } = new();
}

public sealed record ExecutionOptions
{
    public int MaxConcurrency { get; init; } = 4;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool ContinueOnError { get; init; } = false;
}

public sealed record NodeExecutionRequest
{
    public required string ExecutionId { get; init; }
    public required string NodeId { get; init; }
    public required WorkflowState State { get; init; }
    public required int RetryCount { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public sealed record NodeExecutionResult
{
    public required string NodeId { get; init; }
    public required WorkflowState State { get; init; }
    public required bool Success { get; init; }
    public string? Error { get; init; }
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed class ExecutionContext
{
    public string ExecutionId { get; }
    public string WorkflowId { get; }
    public string ThreadId { get; }
    public GraphDefinition Graph { get; }
    public IStateStore StateStore { get; }
    public ExecutionOptions Options { get; }
    public CancellationToken CancellationToken { get; }

    private readonly Channel<NodeExecutionRequest> _executionChannel;
    private readonly Channel<NodeExecutionResult> _resultChannel;
    private readonly List<StateEvent> _events = [];
    private readonly object _eventsLock = new();

    public ExecutionContext(
        string executionId,
        string workflowId,
        string threadId,
        GraphDefinition graph,
        IStateStore stateStore,
        ExecutionOptions options,
        CancellationToken cancellationToken)
    {
        ExecutionId = executionId;
        WorkflowId = workflowId;
        ThreadId = threadId;
        Graph = graph;
        StateStore = stateStore;
        Options = options;
        CancellationToken = cancellationToken;
        _executionChannel = Channel.CreateUnbounded<NodeExecutionRequest>();
        _resultChannel = Channel.CreateUnbounded<NodeExecutionResult>();
    }

    public ChannelReader<NodeExecutionRequest> ExecutionReader => _executionChannel.Reader;
    public ChannelWriter<NodeExecutionRequest> ExecutionWriter => _executionChannel.Writer;
    public ChannelReader<NodeExecutionResult> ResultReader => _resultChannel.Reader;
    public ChannelWriter<NodeExecutionResult> ResultWriter => _resultChannel.Writer;

    public void AddEvent(StateEvent evt)
    {
        lock (_eventsLock)
        {
            _events.Add(evt);
        }
    }

    public IReadOnlyList<StateEvent> GetEvents()
    {
        lock (_eventsLock)
        {
            return _events.ToList();
        }
    }
}
