using IsaacAgent.Core.Services;

namespace IsaacAgent.App.Services;

/// <summary>
/// App-facing seam over Rag Embedding apply — swapable for tests.
/// </summary>
public interface IEmbeddingApply
{
    Task ApplyAsync(IEmbeddingProvider newProvider, CancellationToken ct = default);

    /// <summary>Rebuild the knowledge index with the current embedding provider (exclusive with Apply).</summary>
    Task RebuildAsync(CancellationToken ct = default);
}
