using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraphNexus.Primitives;

public abstract record StateEvent
{
    public required string Id { get; init; }
    public required string ExecutionId { get; init; }
    public required string NodeId { get; init; }
    public required WorkflowState State { get; init; }
    public required StateEventType EventType { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? PreviousHash { get; init; }

    public string ComputeHash()
    {
        var json = JsonSerializer.Serialize(this, StateEventSerializerOptions.Default);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record NodeEnteredEvent : StateEvent
{
    public NodeEnteredEvent(string id, string executionId, string nodeId, WorkflowState state, string? previousHash = null)
    {
        Id = id;
        ExecutionId = executionId;
        NodeId = nodeId;
        State = state;
        EventType = StateEventType.NodeEntered;
        PreviousHash = previousHash;
    }
}

public sealed record NodeExitedEvent : StateEvent
{
    public NodeExitedEvent(string id, string executionId, string nodeId, WorkflowState state, string? previousHash = null)
    {
        Id = id;
        ExecutionId = executionId;
        NodeId = nodeId;
        State = state;
        EventType = StateEventType.NodeExited;
        PreviousHash = previousHash;
    }
}

public sealed record NodeErrorEvent : StateEvent
{
    public required string Error { get; init; }
    public required string StackTrace { get; init; }

    public NodeErrorEvent(string id, string executionId, string nodeId, WorkflowState state, string error, string stackTrace, string? previousHash = null)
    {
        Id = id;
        ExecutionId = executionId;
        NodeId = nodeId;
        State = state;
        EventType = StateEventType.NodeError;
        Error = error;
        StackTrace = stackTrace;
        PreviousHash = previousHash;
    }
}

public sealed record WorkflowCompletedEvent : StateEvent
{
    public WorkflowCompletedEvent(string id, string executionId, string nodeId, WorkflowState state, string? previousHash = null)
    {
        Id = id;
        ExecutionId = executionId;
        NodeId = nodeId;
        State = state;
        EventType = StateEventType.WorkflowCompleted;
        PreviousHash = previousHash;
    }
}

public sealed record WorkflowFailedEvent : StateEvent
{
    public required string Error { get; init; }

    public WorkflowFailedEvent(string id, string executionId, string nodeId, WorkflowState state, string error, string? previousHash = null)
    {
        Id = id;
        ExecutionId = executionId;
        NodeId = nodeId;
        State = state;
        EventType = StateEventType.WorkflowFailed;
        Error = error;
        PreviousHash = previousHash;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StateEventType
{
    NodeEntered,
    NodeExited,
    NodeError,
    WorkflowCompleted,
    WorkflowFailed
}

public static class StateEventSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new StateEventJsonConverter() }
    };
}

public class StateEventJsonConverter : JsonConverter<StateEvent>
{
    public override StateEvent? Read(ref Utf8JsonReader reader, Type typeToRead, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var eventType = root.GetProperty("eventType").GetString();

        return eventType switch
        {
            "nodeEntered" => JsonSerializer.Deserialize<NodeEnteredEvent>(root.GetRawText(), options),
            "nodeExited" => JsonSerializer.Deserialize<NodeExitedEvent>(root.GetRawText(), options),
            "nodeError" => JsonSerializer.Deserialize<NodeErrorEvent>(root.GetRawText(), options),
            "workflowCompleted" => JsonSerializer.Deserialize<WorkflowCompletedEvent>(root.GetRawText(), options),
            "workflowFailed" => JsonSerializer.Deserialize<WorkflowFailedEvent>(root.GetRawText(), options),
            _ => throw new JsonException("Unknown StateEvent type")
        };
    }

    public override void Write(Utf8JsonWriter writer, StateEvent value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
