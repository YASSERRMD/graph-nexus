using GraphNexus.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Core.Nodes;

public interface ITool<in TIn, TOut>
{
    string Name { get; }
    string Description { get; }
    Task<TOut> InvokeAsync(TIn input, CancellationToken cancellationToken = default);
}
