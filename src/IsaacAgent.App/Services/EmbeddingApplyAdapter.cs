using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Embedding;

namespace IsaacAgent.App.Services;

/// <summary>Adapts Rag <see cref="EmbeddingApply"/> to the App <see cref="IEmbeddingApply"/> seam.</summary>
public sealed class EmbeddingApplyAdapter : IEmbeddingApply
{
    private readonly EmbeddingApply _inner;

    public EmbeddingApplyAdapter(EmbeddingApply inner)
    {
        _inner = inner;
    }

    public Task ApplyAsync(IEmbeddingProvider newProvider, CancellationToken ct = default)
        => _inner.ApplyAsync(newProvider, ct);
}
