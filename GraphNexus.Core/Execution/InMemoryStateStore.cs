using System.Collections.Concurrent;
using GraphNexus.Primitives;

namespace GraphNexus.Execution;

public sealed class InMemoryStateStore : IStateStore
{
    private readonly ConcurrentDictionary<string, WorkflowState> _states = [];
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _workflowToStateIds = [];
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _threadToStateIds = [];

    public Task<WorkflowState?> GetStateAsync(string stateId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(stateId, out var state);
        return Task.FromResult(state);
    }

    public Task<IReadOnlyList<WorkflowState>> GetStatesByWorkflowIdAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        if (_workflowToStateIds.TryGetValue(workflowId, out var ids))
        {
            var states = ids.Select(id => _states.TryGetValue(id, out var s) ? s : null)
                .Where(s => s != null)
                .Cast<WorkflowState>()
                .ToList();
            return Task.FromResult<IReadOnlyList<WorkflowState>>(states);
        }
        return Task.FromResult<IReadOnlyList<WorkflowState>>([]);
    }

    public Task<IReadOnlyList<WorkflowState>> GetStatesByThreadIdAsync(string threadId, CancellationToken cancellationToken = default)
    {
        if (_threadToStateIds.TryGetValue(threadId, out var ids))
        {
            var states = ids.Select(id => _states.TryGetValue(id, out var s) ? s : null)
                .Where(s => s != null)
                .Cast<WorkflowState>()
                .ToList();
            return Task.FromResult<IReadOnlyList<WorkflowState>>(states);
        }
        return Task.FromResult<IReadOnlyList<WorkflowState>>([]);
    }

    public Task SaveStateAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        _states[state.Id] = state;

        var workflowIds = _workflowToStateIds.GetOrAdd(state.WorkflowId, _ => []);
        lock (workflowIds)
        {
            if (!workflowIds.Contains(state.Id))
            {
                workflowIds.Add(state.Id);
            }
        }

        var threadIds = _threadToStateIds.GetOrAdd(state.ThreadId, _ => []);
        lock (threadIds)
        {
            if (!threadIds.Contains(state.Id))
            {
                threadIds.Add(state.Id);
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteStateAsync(string stateId, CancellationToken cancellationToken = default)
    {
        if (_states.TryRemove(stateId, out var state))
        {
            if (_workflowToStateIds.TryGetValue(state.WorkflowId, out var workflowIds))
            {
                lock (workflowIds)
                {
                    var list = workflowIds.ToList();
                    list.Remove(stateId);
                    workflowIds.Clear();
                    foreach (var id in list)
                    {
                        workflowIds.Add(id);
                    }
                }
            }

            if (_threadToStateIds.TryGetValue(state.ThreadId, out var threadIds))
            {
                lock (threadIds)
                {
                    var list = threadIds.ToList();
                    list.Remove(stateId);
                    threadIds.Clear();
                    foreach (var id in list)
                    {
                        threadIds.Add(id);
                    }
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string stateId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_states.ContainsKey(stateId));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _states.Clear();
        _workflowToStateIds.Clear();
        _threadToStateIds.Clear();
        return Task.CompletedTask;
    }
}
