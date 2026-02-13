using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraphNexus.Primitives;

public sealed record WorkflowState
{
    public required string Id { get; init; }
    public required string WorkflowId { get; init; }
    public int Step { get; init; }
    public IReadOnlyDictionary<string, object?> Data { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyList<Message> Messages { get; init; } = [];
    public string? CurrentNodeId { get; init; }
    public WorkflowStatus Status { get; init; } = WorkflowStatus.Running;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string? Error { get; init; }

    [JsonConstructor]
    public WorkflowState(
        string id,
        string workflowId,
        int step,
        IReadOnlyDictionary<string, object?> data,
        IReadOnlyList<Message> messages,
        string? currentNodeId,
        WorkflowStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? error)
    {
        Id = id;
        WorkflowId = workflowId;
        Step = step;
        Data = data;
        Messages = messages;
        CurrentNodeId = currentNodeId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Error = error;
    }

    public static WorkflowState Create(string workflowId)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowState(
            Guid.NewGuid().ToString(),
            workflowId,
            0,
            new Dictionary<string, object?>(),
            new List<Message>(),
            null,
            WorkflowStatus.Running,
            now,
            now,
            null
        );
    }

    public WorkflowState WithStep(int step)
    {
        return this with { Step = step, UpdatedAt = DateTimeOffset.UtcNow };
    }

    public WorkflowState WithData(string key, object? value)
    {
        var newData = new Dictionary<string, object?>(Data) { [key] = value };
        return this with { Data = newData, UpdatedAt = DateTimeOffset.UtcNow };
    }

    public WorkflowState WithMessage(Message message)
    {
        var newMessages = new List<Message>(Messages) { message };
        return this with { Messages = newMessages, UpdatedAt = DateTimeOffset.UtcNow };
    }

    public WorkflowState WithCurrentNode(string nodeId)
    {
        return this with { CurrentNodeId = nodeId, UpdatedAt = DateTimeOffset.UtcNow };
    }

    public WorkflowState WithStatus(WorkflowStatus status, string? error = null)
    {
        return this with { Status = status, Error = error, UpdatedAt = DateTimeOffset.UtcNow };
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}
