using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Retrieval;
using IsaacAgent.Rag.Store;

namespace IsaacAgent.Rag.Embedding;

/// <summary>
/// Embedding apply: switch the active embedding provider (any dimensions),
/// invalidate the knowledge index, and rebuild so retrieval matches the new source.
/// </summary>
public sealed class EmbeddingApply
{
    private readonly EmbeddingProviderProxy _proxy;
    private readonly Retriever _retriever;
    private readonly InMemoryVectorStore _store;
    private readonly string _indexPath;

    public EmbeddingApply(
        EmbeddingProviderProxy proxy,
        Retriever retriever,
        InMemoryVectorStore store,
        string indexPath)
    {
        _proxy = proxy;
        _retriever = retriever;
        _store = store;
        _indexPath = indexPath;
    }

    /// <summary>
    /// Replaces the embedding provider, discards the prior knowledge index, and rebuilds.
    /// Dimension changes are allowed; callers must not use the old vectors afterward.
    /// </summary>
    public async Task ApplyAsync(IEmbeddingProvider newProvider, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newProvider);

        _retriever.ResetReady();
        _store.Clear();
        TryDeleteIndexFile();

        _proxy.Replace(newProvider);

        await _retriever.RebuildIndexAsync(ct).ConfigureAwait(false);
    }

    private void TryDeleteIndexFile()
    {
        try
        {
            if (File.Exists(_indexPath))
                File.Delete(_indexPath);
        }
        catch (IOException)
        {
            // Best-effort: RebuildIndexAsync overwrites the file when ready.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
