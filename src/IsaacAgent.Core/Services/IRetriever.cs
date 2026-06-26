using IsaacAgent.Core.Models;

namespace IsaacAgent.Core.Services;

/// <summary>
/// Provides semantic search over an indexed knowledge base.
/// </summary>
public interface IRetriever
{
    /// <summary>True when the index is ready for queries.</summary>
    bool IsReady { get; }

    /// <summary>
    /// Searches the index and returns the top-K matching results.
    /// </summary>
    Task<IReadOnlyList<RetrievalResult>> SearchAsync(string query, int topK = 5, string? categoryFilter = null, CancellationToken ct = default);

    /// <summary>
    /// Ensures the search index is built and ready, creating it if necessary.
    /// </summary>
    Task EnsureIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Rebuilds the search index from scratch.
    /// </summary>
    Task RebuildIndexAsync(CancellationToken ct = default);
}
