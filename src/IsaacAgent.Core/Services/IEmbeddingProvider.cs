namespace IsaacAgent.Core.Services;

/// <summary>
/// Generates vector embeddings for text used in semantic search.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Name of the embedding model.</summary>
    string ModelName { get; }

    /// <summary>Dimensionality of the embedding vectors.</summary>
    int Dimensions { get; }

    /// <summary>
    /// Generates an embedding vector for a single text input.
    /// </summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Generates embedding vectors for a batch of text inputs.
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
