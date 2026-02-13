using GraphNexus.Primitives;

namespace GraphNexus.Nodes;

public interface INode
{
    string Id { get; }
    string Name { get; }

    Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetInputKeys();
    IReadOnlyList<string> GetOutputKeys();
}
