using System.Text.Json;
using GraphNexus.Core.Nodes;
using GraphNexus.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Agents;

public abstract class AgentBase : INode
{
    protected readonly ILlmClient LlmClient;
    protected readonly IReadOnlyList<INode> Tools;
    protected readonly IMemorySelector MemorySelector;
    protected readonly string SystemPrompt;

    public string Id { get; }
    public string Name { get; }

    protected AgentBase(
        string id,
        string name,
        ILlmClient llmClient,
        IReadOnlyList<INode>? tools = null,
        IMemorySelector? memorySelector = null,
        string? systemPrompt = null)
    {
        Id = id;
        Name = name;
        LlmClient = llmClient;
        Tools = tools ?? [];
        MemorySelector = memorySelector ?? new LastNMessagesSelector(10);
        SystemPrompt = systemPrompt ?? GetDefaultSystemPrompt();
    }

    public async Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<Message>(MemorySelector.Select(state));

            var prompt = BuildAgentPrompt(state);
            messages.Add(Message.Create("user", prompt));

            var request = new LlmRequest
            {
                Messages = messages,
                Model = null,
                Temperature = 0.7,
                Tools = Tools.Count > 0 ? BuildToolsDefinition() : null,
                SystemPrompt = SystemPrompt
            };

            var response = await LlmClient.GenerateAsync(request, cancellationToken);

            var newState = state
                .WithData("agent_response", response.Content)
                .WithMessage(Message.Create("assistant", response.Content));

            if (response.ToolCalls != null && response.ToolCalls.Count > 0)
            {
                foreach (var toolCall in response.ToolCalls)
                {
                    var tool = Tools.FirstOrDefault(t => t.Name == toolCall.Name);
                    if (tool != null)
                    {
                        var toolState = newState.WithData("tool_input", toolCall.Arguments);
                        var toolResult = await tool.ExecuteAsync(toolState, cancellationToken);

                        if (toolResult is SuccessResult success)
                        {
                            newState = success.OutputState
                                .WithMessage(Message.Create("tool", $"Tool {toolCall.Name} executed: {success.OutputState.Data["output"]}"));
                        }
                    }
                }
            }

            return new SuccessResult(Id, Guid.NewGuid().ToString(), newState);
        }
        catch (Exception ex)
        {
            return new FailureResult(Id, Guid.NewGuid().ToString(), ex.Message, ex.ToString());
        }
    }

    public IReadOnlyList<string> GetInputKeys() => [];
    public IReadOnlyList<string> GetOutputKeys() => ["agent_response"];

    protected abstract string BuildAgentPrompt(WorkflowState state);

    protected virtual string GetDefaultSystemPrompt()
    {
        return "You are a helpful AI agent that can use tools to accomplish tasks.";
    }

    private Dictionary<string, object> BuildToolsDefinition()
    {
        var tools = new Dictionary<string, object>();
        foreach (var tool in Tools)
        {
            tools[tool.Name] = new
            {
                description = $"Tool: {tool.Name}",
                parameters = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>(),
                    required = new string[] { }
                }
            };
        }
        return tools;
    }
}

public interface IMemorySelector
{
    IReadOnlyList<Message> Select(WorkflowState state);
}

public sealed class LastNMessagesSelector : IMemorySelector
{
    private readonly int _count;

    public LastNMessagesSelector(int count)
    {
        _count = count;
    }

    public IReadOnlyList<Message> Select(WorkflowState state)
    {
        return state.Messages.TakeLast(_count).ToList();
    }
}

public sealed class SemanticSearchSelector : IMemorySelector
{
    private readonly string _query;

    public SemanticSearchSelector(string query)
    {
        _query = query;
    }

    public IReadOnlyList<Message> Select(WorkflowState state)
    {
        return state.Messages.Where(m => m.Content.Contains(_query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
