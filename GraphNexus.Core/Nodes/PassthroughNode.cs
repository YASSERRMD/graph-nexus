using GraphNexus.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Core.Nodes;

public sealed class PassthroughNode : INode
{
    public string Id { get; }
    public string Name { get; }
    private readonly string? _inputKey;
    private readonly string? _outputKey;

    public PassthroughNode(string id, string name, string? inputKey = null, string? outputKey = null)
    {
        Id = id;
        Name = name;
        _inputKey = inputKey;
        _outputKey = outputKey ?? inputKey;
    }

    public Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        var outputState = _inputKey != null && _outputKey != null && state.Data.TryGetValue(_inputKey, out var value)
            ? state.WithData(_outputKey, value)
            : state;

        return Task.FromResult<NodeResult>(new SuccessResult(Id, Guid.NewGuid().ToString(), outputState));
    }

    public IReadOnlyList<string> GetInputKeys() => _inputKey != null ? [_inputKey] : [];
    public IReadOnlyList<string> GetOutputKeys() => _outputKey != null ? [_outputKey] : [];
}
