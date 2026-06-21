using IsaacAgent.Core.Models;

namespace IsaacAgent.Core.Services;

public interface IRetriever
{
    bool IsReady { get; }
    Task<IReadOnlyList<RetrievalResult>> SearchAsync(string query, int topK = 5, string? categoryFilter = null, CancellationToken ct = default);
    Task EnsureIndexAsync(CancellationToken ct = default);
    Task RebuildIndexAsync(CancellationToken ct = default);
}
