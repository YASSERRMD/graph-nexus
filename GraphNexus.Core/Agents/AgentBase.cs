using System.Text.Json;
using GraphNexus.Agents.Policies;
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
    protected readonly IReadOnlyList<IPolicy>? Policies;

    public string Id { get; }
    public string Name { get; }

    protected AgentBase(
        string id,
        string name,
        ILlmClient llmClient,
        IReadOnlyList<INode>? tools = null,
        IMemorySelector? memorySelector = null,
        string? systemPrompt = null,
        IReadOnlyList<IPolicy>? policies = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(llmClient);

        Id = id;
        Name = name;
        LlmClient = llmClient;
        Tools = tools ?? [];
        MemorySelector = memorySelector ?? new LastNMessagesSelector(10);
        SystemPrompt = systemPrompt ?? GetDefaultSystemPrompt();
        Policies = policies;
    }

    public async Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildAgentPrompt(state);

            var userMessage = Message.Create("user", prompt);
            var validationResult = await ValidateMessageAsync(userMessage, cancellationToken);
            if (!validationResult.IsAllowed)
            {
                return new FailureResult(Id, Guid.NewGuid().ToString(), validationResult.Reason ?? "Message rejected by policy", null);
            }

            var messages = new List<Message>(MemorySelector.Select(state));
            messages.Add(userMessage);

            var request = new LlmRequest
            {
                Messages = messages,
                Model = null,
                Temperature = 0.7,
                Tools = Tools.Count > 0 ? BuildToolsDefinition() : null,
                SystemPrompt = SystemPrompt
            };

            var response = await LlmClient.GenerateAsync(request, cancellationToken);

            var assistantMessage = Message.Create("assistant", response.Content);
            var responseValidation = await ValidateMessageAsync(assistantMessage, cancellationToken);
            if (!responseValidation.IsAllowed)
            {
                return new FailureResult(Id, Guid.NewGuid().ToString(), responseValidation.Reason ?? "Response rejected by policy", null);
            }

            var newState = state
                .WithData("agent_response", response.Content)
                .WithMessage(assistantMessage);

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

    private async Task<PolicyResult> ValidateMessageAsync(Message message, CancellationToken cancellationToken)
    {
        if (Policies == null || Policies.Count == 0)
        {
            return PolicyResult.Allowed();
        }

        foreach (var policy in Policies)
        {
            var result = await policy.ValidateAsync(message, cancellationToken);
            if (!result.IsAllowed)
            {
                return result;
            }
        }

        return PolicyResult.Allowed();
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
