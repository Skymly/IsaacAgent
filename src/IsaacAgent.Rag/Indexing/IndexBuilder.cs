using System.Reflection;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Chunking;
using IsaacAgent.Rag.Store;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Rag.Indexing;

public sealed class IndexBuilder
{
    /// <summary>Number of chunks embedded per batch request.</summary>
    private const int EmbeddingBatchSize = 16;

    /// <summary>Interval (in chunks) at which progress is logged during embedding.</summary>
    private const int ProgressReportInterval = 100;

    private readonly IEmbeddingProvider _embedding;
    private readonly InMemoryVectorStore _store;
    private readonly ILogger<IndexBuilder> _logger;
    private readonly string _userKnowledgeDir;
    private readonly Assembly _assembly;

    public IndexBuilder(IEmbeddingProvider embedding, InMemoryVectorStore store, string userKnowledgeDir, ILogger<IndexBuilder> logger)
    {
        _embedding = embedding;
        _store = store;
        _userKnowledgeDir = userKnowledgeDir;
        _logger = logger;
        _assembly = Assembly.GetExecutingAssembly();
    }

    public async Task BuildAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Building RAG index with model {Model} ({Dim}d)", _embedding.ModelName, _embedding.Dimensions);

        var chunks = new List<KnowledgeChunk>();

        // 1. Hardcoded API knowledge (callbacks/classes/enums from C# dictionaries)
        chunks.AddRange(ApiDocChunker.ChunkFromKnowledge());
        _logger.LogInformation("Loaded {Count} chunks from hardcoded API knowledge", chunks.Count);

        // 2. Embedded MkDocs documentation (vanilla + REPENTOGON)
        var vanillaChunks = MarkdownKnowledgeChunker.ChunkFromEmbeddedResources(
            _assembly, "IsaacAgent.Rag.Resources.docs.vanilla", "vanilla",
            MarkdownChunkOptions.ForMkDocsDocs);
        chunks.AddRange(vanillaChunks);
        _logger.LogInformation("Loaded {Count} chunks from embedded vanilla docs", vanillaChunks.Count);

        var repentogonChunks = MarkdownKnowledgeChunker.ChunkFromEmbeddedResources(
            _assembly, "IsaacAgent.Rag.Resources.docs.repentogon", "repentogon",
            MarkdownChunkOptions.ForMkDocsDocs);
        chunks.AddRange(repentogonChunks);
        _logger.LogInformation("Loaded {Count} chunks from embedded REPENTOGON docs", repentogonChunks.Count);

        // 3. User knowledge (app-global Markdown under knowledge/) — MkDocs-style docs chunking
        if (Directory.Exists(_userKnowledgeDir))
        {
            var userChunks = MarkdownKnowledgeChunker.ChunkDirectory(
                _userKnowledgeDir, UserKnowledgePaths.SourceId, MarkdownChunkOptions.ForMkDocsDocs);
            chunks.AddRange(userChunks);
            _logger.LogInformation("Loaded {Count} user knowledge chunks from {Dir}", userChunks.Count, _userKnowledgeDir);
        }

        // 4. Built-in pattern examples (embedded resources)
        var builtinChunks = MarkdownKnowledgeChunker.ChunkFromEmbeddedResources(
            _assembly, "IsaacAgent.Rag.Resources.patterns", "pattern",
            MarkdownChunkOptions.ForPatternsOrExamples);
        chunks.AddRange(builtinChunks);
        if (builtinChunks.Count > 0)
            _logger.LogInformation("Loaded {Count} built-in pattern chunks", builtinChunks.Count);

        _logger.LogInformation("Total chunks to embed: {Count}", chunks.Count);

        var entries = new List<VectorStoreEntry>(chunks.Count);
        var failedChunks = 0;
        for (var i = 0; i < chunks.Count; i += EmbeddingBatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var take = Math.Min(EmbeddingBatchSize, chunks.Count - i);
            var batch = chunks.GetRange(i, take);
            var texts = batch.Select(c => $"{c.Title}\n{c.Content}").ToList();

            try
            {
                var vectors = await _embedding.EmbedBatchAsync(texts, ct);

                for (var j = 0; j < batch.Count; j++)
                    entries.Add(new VectorStoreEntry { Chunk = batch[j], Vector = vectors[j] });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Embedding failed for batch starting at chunk {Index}/{Total}, skipping {BatchCount} chunks", i, chunks.Count, batch.Count);
                failedChunks += batch.Count;
            }

            var done = Math.Min(i + EmbeddingBatchSize, chunks.Count);
            if (done % ProgressReportInterval == 0 || done == chunks.Count)
                _logger.LogInformation("Embedded {Done}/{Total} ({Pct:F1}%)", done, chunks.Count, 100.0 * done / chunks.Count);
        }

        ct.ThrowIfCancellationRequested();
        _store.ReplaceAll(_embedding.ModelName, _embedding.Dimensions, entries);
        _logger.LogInformation("RAG index built: {Count} entries, {Failed} chunks skipped due to embedding failures", entries.Count, failedChunks);
    }
}
