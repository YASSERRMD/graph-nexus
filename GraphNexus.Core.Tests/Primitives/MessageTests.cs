using System.Text.Json;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Primitives;

public class MessageTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Message_SerializationRoundtrip_ShouldPreserveData()
    {
        var message = new Message(
            "msg-123",
            "user",
            "Hello, world!",
            DateTimeOffset.Parse("2024-01-15T10:30:00Z"),
            null,
            null
        );

        var json = JsonSerializer.Serialize(message, Options);
        var deserialized = JsonSerializer.Deserialize<Message>(json, Options);

        Assert.NotNull(deserialized);
        Assert.Equal(message.Id, deserialized.Id);
        Assert.Equal(message.Role, deserialized.Role);
        Assert.Equal(message.Content, deserialized.Content);
        Assert.Equal(message.Timestamp, deserialized.Timestamp);
        Assert.Null(deserialized.ToolCalls);
        Assert.Null(deserialized.Name);
    }

    [Fact]
    public void Message_WithToolCalls_ShouldSerializeCorrectly()
    {
        var toolCall = ToolCall.Create("weather.getCurrent", "{\"location\": \"NYC\"}");
        var toolCalls = new List<ToolCall> { toolCall };
        
        var message = new Message(
            "msg-456",
            "assistant",
            "Let me check the weather...",
            DateTimeOffset.UtcNow,
            toolCalls,
            null
        );

        var json = JsonSerializer.Serialize(message, Options);
        var deserialized = JsonSerializer.Deserialize<Message>(json, Options);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.ToolCalls);
        Assert.Single(deserialized.ToolCalls);
        Assert.Equal("weather.getCurrent", deserialized.ToolCalls[0].Name);
    }

    [Fact]
    public void Message_Create_ShouldGenerateUniqueId()
    {
        var message1 = Message.Create("user", "Test 1");
        var message2 = Message.Create("user", "Test 2");

        Assert.NotEqual(message1.Id, message2.Id);
        Assert.Equal("user", message1.Role);
        Assert.Equal("Test 1", message1.Content);
    }

    [Fact]
    public void Message_WithToolCalls_ShouldReturnNewInstance()
    {
        var original = Message.Create("user", "Test");
        var toolCall = ToolCall.Create("test.tool", "{}");
        var updated = original.WithToolCalls(new List<ToolCall> { toolCall });

        Assert.NotSame(original, updated);
        Assert.Null(original.ToolCalls);
        Assert.NotNull(updated.ToolCalls);
        Assert.Single(updated.ToolCalls);
    }
}
