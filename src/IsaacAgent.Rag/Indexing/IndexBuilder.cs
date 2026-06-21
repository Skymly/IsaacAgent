using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Chunking;
using IsaacAgent.Rag.Store;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Rag.Indexing;

public sealed class IndexBuilder
{
    private readonly IEmbeddingProvider _embedding;
    private readonly InMemoryVectorStore _store;
    private readonly ILogger<IndexBuilder> _logger;
    private readonly string _examplesDir;

    public IndexBuilder(IEmbeddingProvider embedding, InMemoryVectorStore store, string examplesDir, ILogger<IndexBuilder> logger)
    {
        _embedding = embedding;
        _store = store;
        _examplesDir = examplesDir;
        _logger = logger;
    }

    public async Task BuildAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Building RAG index with model {Model} ({Dim}d)", _embedding.ModelName, _embedding.Dimensions);

        var chunks = new List<KnowledgeChunk>();
        chunks.AddRange(ApiDocChunker.ChunkFromKnowledge());

        if (Directory.Exists(_examplesDir))
        {
            var exampleChunks = MarkdownChunker.ChunkDirectory(_examplesDir, "example");
            chunks.AddRange(exampleChunks);
            _logger.LogInformation("Loaded {Count} example chunks from {Dir}", exampleChunks.Count, _examplesDir);
        }

        _logger.LogInformation("Total chunks to embed: {Count}", chunks.Count);

        var entries = new List<VectorStoreEntry>(chunks.Count);
        const int batchSize = 32;
        for (var i = 0; i < chunks.Count; i += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = chunks.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(c => $"{c.Title}\n{c.Content}").ToList();
            var vectors = await _embedding.EmbedBatchAsync(texts, ct);

            for (var j = 0; j < batch.Count; j++)
                entries.Add(new VectorStoreEntry { Chunk = batch[j], Vector = vectors[j] });

            _logger.LogDebug("Embedded {Done}/{Total}", Math.Min(i + batchSize, chunks.Count), chunks.Count);
        }

        _store.ReplaceAll(_embedding.ModelName, _embedding.Dimensions, entries);
        _logger.LogInformation("RAG index built: {Count} entries", entries.Count);
    }
}
