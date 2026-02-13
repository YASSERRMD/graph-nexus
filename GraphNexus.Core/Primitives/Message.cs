using System.Text.Json.Serialization;

namespace GraphNexus.Primitives;

public sealed record Message
{
    public required string Id { get; init; }
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    public string? Name { get; init; }

    [JsonConstructor]
    public Message(string id, string role, string content, DateTimeOffset timestamp, IReadOnlyList<ToolCall>? toolCalls, string? name)
    {
        Id = id;
        Role = role;
        Content = content;
        Timestamp = timestamp;
        ToolCalls = toolCalls;
        Name = name;
    }

    public static Message Create(string role, string content, string? name = null)
    {
        return new Message(
            Guid.NewGuid().ToString(),
            role,
            content,
            DateTimeOffset.UtcNow,
            null,
            name
        );
    }

    public Message WithToolCalls(IReadOnlyList<ToolCall> toolCalls)
    {
        return this with { ToolCalls = toolCalls };
    }
}
