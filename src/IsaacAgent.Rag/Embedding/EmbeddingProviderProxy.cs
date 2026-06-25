using IsaacAgent.Core.Services;

namespace IsaacAgent.Rag.Embedding;

public sealed class EmbeddingProviderProxy : IEmbeddingProvider, IDisposable
{
    private IEmbeddingProvider _inner;
    private volatile bool _disposed;

    public EmbeddingProviderProxy(IEmbeddingProvider inner) => _inner = inner;

    public void Replace(IEmbeddingProvider newProvider)
    {
        if (newProvider.Dimensions != _inner.Dimensions)
        {
            throw new ArgumentException(
                $"Embedding dimension mismatch: current provider has {_inner.Dimensions} dimensions, " +
                $"but replacement has {newProvider.Dimensions}. Vector store compatibility requires identical dimensions.");
        }

        var old = System.Threading.Interlocked.Exchange(ref _inner, newProvider);
        if (old is IDisposable disposable)
            disposable.Dispose();
    }

    public string ModelName => _inner.ModelName;
    public int Dimensions => _inner.Dimensions;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => _inner.EmbedAsync(text, ct);

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => _inner.EmbedBatchAsync(texts, ct);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_inner is IDisposable disposable)
            disposable.Dispose();
    }
}
