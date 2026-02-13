using System.Text.Json;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Primitives;

public class StateEventTests
{
    private static readonly JsonSerializerOptions Options = StateEventSerializerOptions.Default;

    [Fact]
    public void NodeEnteredEvent_ShouldSerializeAndDeserialize()
    {
        var state = WorkflowState.Create("workflow-1").WithCurrentNode("node-1");
        var evt = new NodeEnteredEvent("evt-1", "exec-1", "node-1", state);

        var json = JsonSerializer.Serialize(evt, Options);
        var deserialized = JsonSerializer.Deserialize<StateEvent>(json, Options);

        Assert.NotNull(deserialized);
        Assert.IsType<NodeEnteredEvent>(deserialized);
        Assert.Equal(StateEventType.NodeEntered, deserialized.EventType);
    }

    [Fact]
    public void NodeExitedEvent_ShouldSerializeAndDeserialize()
    {
        var state = WorkflowState.Create("workflow-1").WithCurrentNode("node-1");
        var evt = new NodeExitedEvent("evt-2", "exec-1", "node-1", state);

        var json = JsonSerializer.Serialize(evt, Options);
        var deserialized = JsonSerializer.Deserialize<StateEvent>(json, Options);

        Assert.NotNull(deserialized);
        Assert.IsType<NodeExitedEvent>(deserialized);
        Assert.Equal(StateEventType.NodeExited, deserialized.EventType);
    }

    [Fact]
    public void NodeErrorEvent_ShouldSerializeAndDeserialize()
    {
        var state = WorkflowState.Create("workflow-1").WithCurrentNode("node-1");
        var evt = new NodeErrorEvent("evt-3", "exec-1", "node-1", state, "Division by zero", "stack trace here");

        var json = JsonSerializer.Serialize(evt, Options);
        var deserialized = JsonSerializer.Deserialize<StateEvent>(json, Options);

        Assert.NotNull(deserialized);
        Assert.IsType<NodeErrorEvent>(deserialized);
        var error = (NodeErrorEvent)deserialized;
        Assert.Equal("Division by zero", error.Error);
        Assert.Equal(StateEventType.NodeError, error.EventType);
    }

    [Fact]
    public void WorkflowCompletedEvent_ShouldSerializeAndDeserialize()
    {
        var state = WorkflowState.Create("workflow-1").WithStatus(WorkflowStatus.Completed);
        var evt = new WorkflowCompletedEvent("evt-4", "exec-1", "end-node", state);

        var json = JsonSerializer.Serialize(evt, Options);
        var deserialized = JsonSerializer.Deserialize<StateEvent>(json, Options);

        Assert.NotNull(deserialized);
        Assert.IsType<WorkflowCompletedEvent>(deserialized);
        Assert.Equal(StateEventType.WorkflowCompleted, deserialized.EventType);
    }

    [Fact]
    public void StateEvent_ComputeHash_ShouldReturnConsistentHash()
    {
        var state = WorkflowState.Create("workflow-1");
        var evt = new NodeEnteredEvent("evt-1", "exec-1", "node-1", state);

        var hash1 = evt.ComputeHash();
        var hash2 = evt.ComputeHash();

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }
}
