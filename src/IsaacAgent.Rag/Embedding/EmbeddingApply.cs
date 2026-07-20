using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Retrieval;
using IsaacAgent.Rag.Store;

namespace IsaacAgent.Rag.Embedding;

/// <summary>
/// Embedding apply: switch the active embedding provider (any dimensions),
/// invalidate the knowledge index, and rebuild so retrieval matches the new source.
/// A newer apply (or an external/shutdown token) cancels any in-flight rebuild.
/// </summary>
public sealed class EmbeddingApply
{
    private readonly EmbeddingProviderProxy _proxy;
    private readonly Retriever _retriever;
    private readonly InMemoryVectorStore _store;
    private readonly string _indexPath;
    private readonly object _gate = new();
    private CancellationTokenSource? _inFlightCts;

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
    /// Cancels any in-flight apply first so the final index matches this call's provider.
    /// </summary>
    public async Task ApplyAsync(IEmbeddingProvider newProvider, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newProvider);

        CancellationTokenSource linkedCts;
        lock (_gate)
        {
            _inFlightCts?.Cancel();
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _inFlightCts = linkedCts;
        }

        try
        {
            _retriever.ResetReady();
            _store.Clear();
            TryDeleteIndexFile();

            _proxy.Replace(newProvider);

            await _retriever.RebuildIndexAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Only clear ready if this apply is still current; a newer apply may
            // already have finished and marked the knowledge index ready.
            lock (_gate)
            {
                if (ReferenceEquals(_inFlightCts, linkedCts))
                    _retriever.ResetReady();
            }

            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_inFlightCts, linkedCts))
                    _inFlightCts = null;
            }

            linkedCts.Dispose();
        }
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
