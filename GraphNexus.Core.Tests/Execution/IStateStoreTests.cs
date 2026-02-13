using GraphNexus.Execution;
using GraphNexus.Primitives;
using Moq;
using Xunit;

namespace GraphNexus.Core.Tests.Execution;

public class MockStateStore : IStateStore
{
    private readonly Dictionary<string, WorkflowState> _states = [];
    private readonly Dictionary<string, List<string>> _workflowToStates = [];
    private readonly Dictionary<string, List<string>> _threadToStates = [];

    public Task<WorkflowState?> GetStateAsync(string stateId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(stateId, out var state);
        return Task.FromResult(state);
    }

    public Task<IReadOnlyList<WorkflowState>> GetStatesByWorkflowIdAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        if (_workflowToStates.TryGetValue(workflowId, out var ids))
        {
            var states = ids.Select(id => _states[id]).ToList();
            return Task.FromResult<IReadOnlyList<WorkflowState>>(states);
        }
        return Task.FromResult<IReadOnlyList<WorkflowState>>([]);
    }

    public Task<IReadOnlyList<WorkflowState>> GetStatesByThreadIdAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (_threadToStates.TryGetValue(threadId, out var ids))
        {
            var states = ids.Select(id => _states[id]).ToList();
            return Task.FromResult<IReadOnlyList<WorkflowState>>(states);
        }
        return Task.FromResult<IReadOnlyList<WorkflowState>>([]);
    }

    public Task SaveStateAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        _states[state.Id] = state;
        if (!_workflowToStates.ContainsKey(state.WorkflowId))
        {
            _workflowToStates[state.WorkflowId] = [];
        }
        _workflowToStates[state.WorkflowId].Add(state.Id);

        if (!_threadToStates.ContainsKey(state.ThreadId))
        {
            _threadToStates[state.ThreadId] = [];
        }
        _threadToStates[state.ThreadId].Add(state.Id);

        return Task.CompletedTask;
    }

    public Task DeleteStateAsync(string stateId, CancellationToken cancellationToken = default)
    {
        if (_states.TryGetValue(stateId, out var state))
        {
            _states.Remove(stateId);
            _workflowToStates[state.WorkflowId].Remove(stateId);
            _threadToStates[state.ThreadId].Remove(stateId);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string stateId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_states.ContainsKey(stateId));
    }
}

public class IStateStoreTests
{
    [Fact]
    public async Task GetStateAsync_WhenNotFound_ShouldReturnNull()
    {
        var store = new MockStateStore();

        var result = await store.GetStateAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveStateAsync_ShouldStoreState()
    {
        var store = new MockStateStore();
        var state = WorkflowState.Create("workflow-1");

        await store.SaveStateAsync(state);
        var result = await store.GetStateAsync(state.Id);

        Assert.NotNull(result);
        Assert.Equal(state.Id, result.Id);
    }

    [Fact]
    public async Task GetStatesByWorkflowIdAsync_ShouldReturnWorkflowStates()
    {
        var store = new MockStateStore();
        var state1 = WorkflowState.Create("workflow-1");
        var state2 = WorkflowState.Create("workflow-1");

        await store.SaveStateAsync(state1);
        await store.SaveStateAsync(state2);
        var results = await store.GetStatesByWorkflowIdAsync("workflow-1");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetStatesByThreadIdAsync_ShouldReturnThreadStates()
    {
        var store = new MockStateStore();
        var threadId = "thread-123";
        var state1 = WorkflowState.Create("workflow-1", threadId);
        var state2 = WorkflowState.Create("workflow-2", threadId);

        await store.SaveStateAsync(state1);
        await store.SaveStateAsync(state2);
        var results = await store.GetStatesByThreadIdAsync(threadId);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task DeleteStateAsync_ShouldRemoveState()
    {
        var store = new MockStateStore();
        var state = WorkflowState.Create("workflow-1");

        await store.SaveStateAsync(state);
        await store.DeleteStateAsync(state.Id);
        var result = await store.GetStateAsync(state.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnCorrectStatus()
    {
        var store = new MockStateStore();
        var state = WorkflowState.Create("workflow-1");

        var before = await store.ExistsAsync(state.Id);
        await store.SaveStateAsync(state);
        var after = await store.ExistsAsync(state.Id);

        Assert.False(before);
        Assert.True(after);
    }
}
