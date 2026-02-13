using System.Text.Json;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Primitives;

public class WorkflowStateTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void WorkflowState_Create_ShouldInitializeCorrectly()
    {
        var state = WorkflowState.Create("workflow-1");

        Assert.NotNull(state.Id);
        Assert.Equal("workflow-1", state.WorkflowId);
        Assert.NotNull(state.ThreadId);
        Assert.Equal(0, state.Step);
        Assert.Empty(state.Data);
        Assert.Empty(state.Messages);
        Assert.Null(state.CurrentNodeId);
        Assert.Equal(WorkflowStatus.Running, state.Status);
        Assert.Null(state.Error);
    }

    [Fact]
    public void WorkflowState_Create_WithThreadId_ShouldUseProvidedThreadId()
    {
        var state = WorkflowState.Create("workflow-1", "thread-123");

        Assert.Equal("thread-123", state.ThreadId);
    }

    [Fact]
    public void WorkflowState_SerializationRoundtrip_ShouldPreserveData()
    {
        var state = new WorkflowState(
            "state-123",
            "workflow-1",
            "thread-456",
            5,
            new Dictionary<string, object?> { ["key"] = "value" },
            new List<Message> { Message.Create("user", "Hello") },
            "node-1",
            WorkflowStatus.Running,
            DateTimeOffset.Parse("2024-01-15T10:30:00Z"),
            DateTimeOffset.Parse("2024-01-15T10:35:00Z"),
            null
        );

        var json = JsonSerializer.Serialize(state, Options);
        var deserialized = JsonSerializer.Deserialize<WorkflowState>(json, Options);

        Assert.NotNull(deserialized);
        Assert.Equal(state.Id, deserialized.Id);
        Assert.Equal(state.WorkflowId, deserialized.WorkflowId);
        Assert.Equal(state.ThreadId, deserialized.ThreadId);
        Assert.Equal(state.Step, deserialized.Step);
        Assert.Equal("value", deserialized.Data["key"]);
        Assert.Single(deserialized.Messages);
        Assert.Equal("node-1", deserialized.CurrentNodeId);
        Assert.Equal(WorkflowStatus.Running, deserialized.Status);
    }

    [Fact]
    public void WorkflowState_WithStep_ShouldReturnNewInstance()
    {
        var original = WorkflowState.Create("workflow-1", "thread-1");
        var updated = original.WithStep(10);

        Assert.NotSame(original, updated);
        Assert.Equal(0, original.Step);
        Assert.Equal(10, updated.Step);
    }

    [Fact]
    public void WorkflowState_WithData_ShouldMergeData()
    {
        var original = WorkflowState.Create("workflow-1", "thread-1")
            .WithData("key1", "value1");
        var updated = original.WithData("key2", "value2");

        Assert.Equal("value1", updated.Data["key1"]);
        Assert.Equal("value2", updated.Data["key2"]);
    }

    [Fact]
    public void WorkflowState_WithMessage_ShouldAppendMessage()
    {
        var original = WorkflowState.Create("workflow-1", "thread-1");
        var message = Message.Create("user", "Test message");
        var updated = original.WithMessage(message);

        Assert.Empty(original.Messages);
        Assert.Single(updated.Messages);
        Assert.Equal(message, updated.Messages[0]);
    }

    [Fact]
    public void WorkflowState_WithStatus_ShouldUpdateStatusAndError()
    {
        var original = WorkflowState.Create("workflow-1", "thread-1");
        var failed = original.WithStatus(WorkflowStatus.Failed, "Something went wrong");

        Assert.Equal(WorkflowStatus.Running, original.Status);
        Assert.Equal(WorkflowStatus.Failed, failed.Status);
        Assert.Equal("Something went wrong", failed.Error);
    }
}
