using GraphNexus.Primitives;

namespace GraphNexus.Core.Nodes;

public interface ILlmClient
{
    Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> GenerateStreamingAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

public sealed record LlmRequest
{
    public required IReadOnlyList<Message> Messages { get; init; }
    public string? Model { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
    public IReadOnlyDictionary<string, object>? Tools { get; init; }
    public string? SystemPrompt { get; init; }
}

public sealed record LlmResponse
{
    public required string Content { get; init; }
    public string? Model { get; init; }
    public int TokensUsed { get; init; }
    public string? FinishReason { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
}
