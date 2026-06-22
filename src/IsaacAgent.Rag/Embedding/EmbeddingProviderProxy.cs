using IsaacAgent.Core.Services;

namespace IsaacAgent.Rag.Embedding;

public sealed class EmbeddingProviderProxy : IEmbeddingProvider
{
    private volatile IEmbeddingProvider _inner;

    public EmbeddingProviderProxy(IEmbeddingProvider inner) => _inner = inner;

    public void Replace(IEmbeddingProvider newProvider) => _inner = newProvider;

    public string ModelName => _inner.ModelName;
    public int Dimensions => _inner.Dimensions;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => _inner.EmbedAsync(text, ct);

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => _inner.EmbedBatchAsync(texts, ct);
}
