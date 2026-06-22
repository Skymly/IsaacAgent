using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Indexing;
using IsaacAgent.Rag.Store;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Rag.Retrieval;

public sealed class Retriever : IRetriever
{
    private readonly IEmbeddingProvider _embedding;
    private readonly InMemoryVectorStore _store;
    private readonly IndexBuilder _builder;
    private readonly ILogger<Retriever> _logger;
    private readonly string _indexPath;
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private int _isReady;

    public Retriever(
        IEmbeddingProvider embedding,
        InMemoryVectorStore store,
        IndexBuilder builder,
        string indexPath,
        ILogger<Retriever> logger)
    {
        _embedding = embedding;
        _store = store;
        _builder = builder;
        _indexPath = indexPath;
        _logger = logger;
    }

    public bool IsReady => Interlocked.CompareExchange(ref _isReady, 0, 0) == 1;

    public void ResetReady() => Interlocked.Exchange(ref _isReady, 0);

    public async Task EnsureIndexAsync(CancellationToken ct = default)
    {
        if (IsReady) return;

        await _buildLock.WaitAsync(ct);
        try
        {
            if (IsReady) return;

            if (await _store.LoadAsync(_indexPath, ct))
            {
                if (_store.ModelName == _embedding.ModelName && _store.Dimensions == _embedding.Dimensions && _store.Count > 0)
                {
                    _logger.LogInformation("Loaded RAG index from {Path} ({Count} entries)", _indexPath, _store.Count);
                    Interlocked.Exchange(ref _isReady, 1);
                    return;
                }
                _logger.LogInformation("Index model/dim mismatch or empty, rebuilding");
            }

            await _builder.BuildAsync(ct);
            await _store.SaveAsync(_indexPath, ct);
            Interlocked.Exchange(ref _isReady, 1);
        }
        finally
        {
            _buildLock.Release();
        }
    }

    public async Task RebuildIndexAsync(CancellationToken ct = default)
    {
        await _buildLock.WaitAsync(ct);
        try
        {
            Interlocked.Exchange(ref _isReady, 0);
            await _builder.BuildAsync(ct);
            await _store.SaveAsync(_indexPath, ct);
            Interlocked.Exchange(ref _isReady, 1);
        }
        finally
        {
            _buildLock.Release();
        }
    }

    public async Task<IReadOnlyList<RetrievalResult>> SearchAsync(string query, int topK = 5, string? categoryFilter = null, CancellationToken ct = default)
    {
        await EnsureIndexAsync(ct);
        if (_store.Count == 0) return [];

        var queryVector = await _embedding.EmbedAsync(query, ct);
        return _store.Search(queryVector, topK, categoryFilter);
    }
}
