using GraphNexus.Primitives;

namespace GraphNexus.Execution;

public interface IStateStore
{
    Task<WorkflowState?> GetStateAsync(string stateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowState>> GetStatesByWorkflowIdAsync(string workflowId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowState>> GetStatesByThreadIdAsync(string threadId, CancellationToken cancellationToken = default);
    Task SaveStateAsync(WorkflowState state, CancellationToken cancellationToken = default);
    Task DeleteStateAsync(string stateId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string stateId, CancellationToken cancellationToken = default);
}
