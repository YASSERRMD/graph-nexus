using System.Text.Json;
using GraphNexus.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Core.Nodes;

public sealed class ToolNode<TIn, TOut> : INode
{
    private readonly ITool<TIn, TOut> _tool;
    private readonly Func<WorkflowState, TIn>? _inputMapper;
    private readonly Func<WorkflowState, TOut, WorkflowState> _outputMapper;

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<string> InputKeys { get; }
    public IReadOnlyList<string> OutputKeys { get; }

    public ToolNode(
        string id,
        string name,
        ITool<TIn, TOut> tool,
        Func<WorkflowState, TIn>? inputMapper = null,
        Func<WorkflowState, TOut, WorkflowState>? outputMapper = null,
        IReadOnlyList<string>? inputKeys = null,
        IReadOnlyList<string>? outputKeys = null)
    {
        Id = id;
        Name = name;
        _tool = tool;
        _inputMapper = inputMapper ?? DefaultInputMapper;
        _outputMapper = outputMapper ?? DefaultOutputMapper;
        InputKeys = inputKeys ?? [];
        OutputKeys = outputKeys ?? ["output"];
    }

    public async Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var input = _inputMapper(state);
            var output = await _tool.InvokeAsync(input, cancellationToken);
            var newState = _outputMapper(state, output);

            return new SuccessResult(Id, Guid.NewGuid().ToString(), newState);
        }
        catch (Exception ex)
        {
            return new FailureResult(Id, Guid.NewGuid().ToString(), ex.Message, ex.ToString());
        }
    }

    public IReadOnlyList<string> GetInputKeys() => InputKeys;
    public IReadOnlyList<string> GetOutputKeys() => OutputKeys;

    private static TIn DefaultInputMapper(WorkflowState state)
    {
        var json = JsonSerializer.Serialize(state.Data);
        return JsonSerializer.Deserialize<TIn>(json) ?? throw new InvalidOperationException("Failed to deserialize input");
    }

    private static WorkflowState DefaultOutputMapper(WorkflowState state, TOut output)
    {
        return state.WithData("output", output);
    }
}
