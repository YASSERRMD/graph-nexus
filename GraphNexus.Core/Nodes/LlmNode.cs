using System.Text.RegularExpressions;
using GraphNexus.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Core.Nodes;

public sealed class LlmNode : INode
{
    private readonly ILlmClient _client;
    private readonly string _promptTemplate;
    private readonly string? _model;
    private readonly string _outputKey;
    private readonly double? _temperature;
    private readonly int? _maxTokens;

    public string Id { get; }
    public string Name { get; }

    public LlmNode(
        string id,
        string name,
        ILlmClient client,
        string promptTemplate,
        string? model = null,
        string outputKey = "llm_output",
        double? temperature = null,
        int? maxTokens = null)
    {
        Id = id;
        Name = name;
        _client = client;
        _promptTemplate = promptTemplate;
        _model = model;
        _outputKey = outputKey;
        _temperature = temperature;
        _maxTokens = maxTokens;
    }

    public async Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = RenderTemplate(state);

            var messages = new List<Message>
            {
                Message.Create("user", prompt)
            };

            var request = new LlmRequest
            {
                Messages = messages,
                Model = _model,
                Temperature = _temperature,
                MaxTokens = _maxTokens
            };

            var response = await _client.GenerateAsync(request, cancellationToken);
            var newState = state.WithData(_outputKey, response.Content);

            return new SuccessResult(Id, Guid.NewGuid().ToString(), newState);
        }
        catch (Exception ex)
        {
            return new FailureResult(Id, Guid.NewGuid().ToString(), ex.Message, ex.ToString());
        }
    }

    public IReadOnlyList<string> GetInputKeys() => ExtractTemplateVariables(_promptTemplate);
    public IReadOnlyList<string> GetOutputKeys() => [_outputKey];

    private string RenderTemplate(WorkflowState state)
    {
        var template = _promptTemplate;
        foreach (var (key, value) in state.Data)
        {
            var placeholder = $"{{{{{key}}}}}";
            template = template.Replace(placeholder, value?.ToString() ?? "");
        }
        return template;
    }

    private static IReadOnlyList<string> ExtractTemplateVariables(string template)
    {
        var matches = Regex.Matches(template, @"\{\{(\w+)\}\}");
        return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
    }
}
