using GraphNexus.Primitives;

namespace GraphNexus.Core.Nodes;

public interface ILlmClient
{
    Task<string> GenerateAsync(string prompt, string? model = null, CancellationToken cancellationToken = default);
}
