using GraphNexus.Execution;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Execution;

public class StateEventHelpersTests
{
    [Fact]
    public void CreatePatch_WhenStepChanges_ShouldAddStepOperation()
    {
        var oldState = WorkflowState.Create("workflow-1", "thread-1");
        var newState = oldState.WithStep(5);

        var patch = StateEventHelpers.CreatePatch(oldState, newState);

        Assert.Contains(patch, op => op.Path == "/step" && (int)op.Value! == 5);
    }

    [Fact]
    public void CreatePatch_WhenDataChanges_ShouldAddDataOperations()
    {
        var oldState = WorkflowState.Create("workflow-1", "thread-1");
        var newState = oldState.WithData("key", "value");

        var patch = StateEventHelpers.CreatePatch(oldState, newState);

        Assert.Contains(patch, op => op.Path == "/data/key" && (string)op.Value! == "value");
    }

    [Fact]
    public void CreatePatch_WhenNewMessageAdded_ShouldAddMessageOperation()
    {
        var oldState = WorkflowState.Create("workflow-1", "thread-1");
        var newState = oldState.WithMessage(Message.Create("user", "Hello"));

        var patch = StateEventHelpers.CreatePatch(oldState, newState);

        Assert.Contains(patch, op => op.Path == "/messages");
    }

    [Fact]
    public void ComputeStateHash_ShouldReturnConsistentHash()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1").WithStep(10);

        var hash1 = StateEventHelpers.ComputeStateHash(state);
        var hash2 = StateEventHelpers.ComputeStateHash(state);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }

    [Fact]
    public void ComputeStateHash_DifferentStates_ShouldReturnDifferentHashes()
    {
        var state1 = WorkflowState.Create("workflow-1", "thread-1").WithStep(1);
        var state2 = WorkflowState.Create("workflow-1", "thread-1").WithStep(2);

        var hash1 = StateEventHelpers.ComputeStateHash(state1);
        var hash2 = StateEventHelpers.ComputeStateHash(state2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ValidateHash_WithCorrectHash_ShouldReturnTrue()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1").WithStep(10);
        var hash = StateEventHelpers.ComputeStateHash(state);

        var isValid = StateEventHelpers.ValidateHash(state, hash);

        Assert.True(isValid);
    }

    [Fact]
    public void ValidateHash_WithWrongHash_ShouldReturnFalse()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1").WithStep(10);

        var isValid = StateEventHelpers.ValidateHash(state, "invalid-hash");

        Assert.False(isValid);
    }

    [Fact]
    public void CreateEnteredEvent_ShouldCreateValidEvent()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1").WithCurrentNode("node-1");

        var evt = StateEventHelpers.CreateEnteredEvent("exec-1", "node-1", state);

        Assert.IsType<NodeEnteredEvent>(evt);
        Assert.Equal("exec-1", evt.ExecutionId);
        Assert.Equal("node-1", evt.NodeId);
    }

    [Fact]
    public void CreateExitedEvent_ShouldCreateValidEvent()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1").WithCurrentNode("node-1");

        var evt = StateEventHelpers.CreateExitedEvent("exec-1", "node-1", state);

        Assert.IsType<NodeExitedEvent>(evt);
        Assert.Equal(StateEventType.NodeExited, evt.EventType);
    }

    [Fact]
    public void CreateErrorEvent_ShouldCreateValidEvent()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1").WithCurrentNode("node-1");
        var ex = new InvalidOperationException("Test error");

        var evt = StateEventHelpers.CreateErrorEvent("exec-1", "node-1", state, ex);

        Assert.IsType<NodeErrorEvent>(evt);
        var errorEvt = (NodeErrorEvent)evt;
        Assert.Equal("Test error", errorEvt.Error);
    }

    [Fact]
    public void CreateCompletedEvent_ShouldCreateValidEvent()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1").WithStatus(WorkflowStatus.Completed);

        var evt = StateEventHelpers.CreateCompletedEvent("exec-1", "end", state);

        Assert.IsType<WorkflowCompletedEvent>(evt);
        Assert.Equal(StateEventType.WorkflowCompleted, evt.EventType);
    }

    [Fact]
    public void CreateFailedEvent_ShouldCreateValidEvent()
    {
        var state = WorkflowState.Create("workflow-1", "thread-1").WithStatus(WorkflowStatus.Failed, "Error");

        var evt = StateEventHelpers.CreateFailedEvent("exec-1", "node-1", state, "Error");

        Assert.IsType<WorkflowFailedEvent>(evt);
        Assert.Equal(StateEventType.WorkflowFailed, evt.EventType);
    }
}
