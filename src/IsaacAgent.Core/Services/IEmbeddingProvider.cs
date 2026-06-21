namespace IsaacAgent.Core.Services;

public interface IEmbeddingProvider
{
    string ModelName { get; }
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
