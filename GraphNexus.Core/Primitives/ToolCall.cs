using System.Text.Json.Serialization;

namespace GraphNexus.Primitives;

public sealed record ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; }
    public string? Output { get; init; }
    public ToolCallStatus Status { get; init; } = ToolCallStatus.Pending;
    public DateTimeOffset? CompletedAt { get; init; }

    [JsonConstructor]
    public ToolCall(string id, string name, string arguments, string? output, ToolCallStatus status, DateTimeOffset? completedAt)
    {
        Id = id;
        Name = name;
        Arguments = arguments;
        Output = output;
        Status = status;
        CompletedAt = completedAt;
    }

    public static ToolCall Create(string name, string arguments)
    {
        return new ToolCall(
            Guid.NewGuid().ToString(),
            name,
            arguments,
            null,
            ToolCallStatus.Pending,
            null
        );
    }

    public ToolCall WithOutput(string output)
    {
        return this with { Output = output, Status = ToolCallStatus.Completed, CompletedAt = DateTimeOffset.UtcNow };
    }

    public ToolCall WithError(string error)
    {
        return this with { Output = error, Status = ToolCallStatus.Error, CompletedAt = DateTimeOffset.UtcNow };
    }
}

public enum ToolCallStatus
{
    Pending,
    Running,
    Completed,
    Error
}
