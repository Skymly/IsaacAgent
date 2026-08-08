using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Chunking;
using IsaacAgent.Rag.Indexing;
using IsaacAgent.Rag.Store;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class IndexBuilderUserKnowledgeTests
{
    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        public string ModelName => "test-model";
        public int Dimensions => 3;

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[] { 1f, 0f, 0f });

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[] { 1f, 0f, 0f }).ToList());
    }

    [Fact]
    public async Task BuildAsync_IncludesUserKnowledgeMarkdownWithSourceUser()
    {
        var knowledgeDir = Path.Combine(Path.GetTempPath(), $"isaac_uk_{Guid.NewGuid():N}");
        Directory.CreateDirectory(knowledgeDir);
        const string marker = "USER_KNOWLEDGE_MARKER_R012_UNIQUE";
        File.WriteAllText(Path.Combine(knowledgeDir, "custom.md"), $"# Custom\n\n{marker}\n");
        File.WriteAllText(Path.Combine(knowledgeDir, "ignore.txt"), "should not be indexed");

        var embedding = new StubEmbeddingProvider();
        var store = new InMemoryVectorStore();
        var builder = new IndexBuilder(embedding, store, knowledgeDir, Mock.Of<ILogger<IndexBuilder>>());

        try
        {
            await builder.BuildAsync();

            var hits = store.Search(new float[] { 1f, 0f, 0f }, topK: store.Count);
            var userHits = hits.Where(r => r.Chunk.Source == UserKnowledgePaths.SourceId).ToList();

            Assert.NotEmpty(userHits);
            Assert.Contains(userHits, r => r.Chunk.Content.Contains(marker));
            Assert.DoesNotContain(userHits, r => r.Chunk.Content.Contains("should not be indexed"));
        }
        finally
        {
            Directory.Delete(knowledgeDir, true);
        }
    }

    [Fact]
    public void ChunkDirectory_WithMkDocsPreset_UsesUserSourceAndIgnoresNonMarkdown()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"isaac_chunk_uk_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.md"), "# Title\n\nBody text for chunking.\n");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "plain text");

            var chunks = MarkdownKnowledgeChunker.ChunkDirectory(
                dir, UserKnowledgePaths.SourceId, MarkdownChunkOptions.ForMkDocsDocs);

            Assert.NotEmpty(chunks);
            Assert.All(chunks, c => Assert.Equal(UserKnowledgePaths.SourceId, c.Source));
            Assert.DoesNotContain(chunks, c => c.Content.Contains("plain text"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
