using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Chunking;
using IsaacAgent.Rag.Store;
using IsaacAgent.Rag.Embedding;
using Xunit;

namespace IsaacAgent.Tests;

public class RagChunkerTests
{
    [Fact]
    public void ApiDocChunker_ProducesCallbacksClassesEnums()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();

        Assert.NotEmpty(chunks);
        Assert.Contains(chunks, c => c.Category == "callback");
        Assert.Contains(chunks, c => c.Category == "class");
        Assert.Contains(chunks, c => c.Category == "enum");

        var mcUpdate = chunks.FirstOrDefault(c => c.Id == "callback:MC_POST_UPDATE");
        Assert.NotNull(mcUpdate);
        Assert.Contains("MC_POST_UPDATE", mcUpdate!.Content);
        Assert.Equal("vanilla", mcUpdate.Source);
    }

    [Fact]
    public void MarkdownChunker_ParsesFrontMatter()
    {
        var md = """
            ---
            title: Custom Collectible
            category: example
            tags: item, collectible
            ---

            # Overview
            This shows how to make a custom item.

            # Code
            ```lua
            local mod = RegisterMod("MyMod", 1)
            ```
            """;

        var chunks = MarkdownChunker.ChunkMarkdown(md, "custom_item.md", "example");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("example", c.Source));
        Assert.All(chunks, c => Assert.Equal("example", c.Category));
        var first = chunks[0];
        Assert.StartsWith("Custom Collectible", first.Title);
        Assert.Equal("item, collectible", first.Metadata["tags"]);
    }

    [Fact]
    public void MarkdownChunker_SplitsByHeadings()
    {
        var md = """
            # Section A
            Content A

            # Section B
            Content B
            """;

        var chunks = MarkdownChunker.ChunkMarkdown(md, "test.md", "example");

        Assert.True(chunks.Count >= 2);
        Assert.Contains(chunks, c => c.Title.Contains("Section A"));
        Assert.Contains(chunks, c => c.Title.Contains("Section B"));
    }
}

public class InMemoryVectorStoreTests
{
    [Fact]
    public void Search_RanksByCosineSimilarity()
    {
        var store = new InMemoryVectorStore();
        var entries = new List<VectorStoreEntry>
        {
            new()
            {
                Chunk = new IsaacAgent.Core.Models.KnowledgeChunk
                {
                    Id = "a", Source = "test", Category = "x", Title = "A", Content = "apple"
                },
                Vector = [1f, 0f, 0f]
            },
            new()
            {
                Chunk = new IsaacAgent.Core.Models.KnowledgeChunk
                {
                    Id = "b", Source = "test", Category = "x", Title = "B", Content = "banana"
                },
                Vector = [0f, 1f, 0f]
            }
        };
        store.ReplaceAll("test-model", 3, entries);

        var results = store.Search([1f, 0.1f, 0f], topK: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("a", results[0].Chunk.Id);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public void Search_CategoryFilter()
    {
        var store = new InMemoryVectorStore();
        var entries = new List<VectorStoreEntry>
        {
            new()
            {
                Chunk = new IsaacAgent.Core.Models.KnowledgeChunk
                {
                    Id = "a", Source = "test", Category = "callback", Title = "A", Content = "a"
                },
                Vector = [1f, 0f]
            },
            new()
            {
                Chunk = new IsaacAgent.Core.Models.KnowledgeChunk
                {
                    Id = "b", Source = "test", Category = "class", Title = "B", Content = "b"
                },
                Vector = [1f, 0f]
            }
        };
        store.ReplaceAll("test-model", 2, entries);

        var results = store.Search([1f, 0f], topK: 5, categoryFilter: "callback");

        Assert.Single(results);
        Assert.Equal("a", results[0].Chunk.Id);
    }

    [Fact]
    public async Task SaveLoad_RoundTrips()
    {
        var store = new InMemoryVectorStore();
        var entries = new List<VectorStoreEntry>
        {
            new()
            {
                Chunk = new IsaacAgent.Core.Models.KnowledgeChunk
                {
                    Id = "test:1", Source = "vanilla", Category = "callback",
                    Title = "MC_TEST", Content = "Test content",
                    Metadata = { ["key"] = "value" }
                },
                Vector = [0.5f, 0.5f]
            }
        };
        store.ReplaceAll("test-model", 2, entries);

        var tempPath = Path.Combine(Path.GetTempPath(), $"isaac_rag_test_{Guid.NewGuid():N}.bin");
        try
        {
            await store.SaveAsync(tempPath);
            var loaded = new InMemoryVectorStore();
            var ok = await loaded.LoadAsync(tempPath);

            Assert.True(ok);
            Assert.Equal("test-model", loaded.ModelName);
            Assert.Equal(2, loaded.Dimensions);
            Assert.Equal(1, loaded.Count);

            var results = loaded.Search([0.5f, 0.5f], topK: 1);
            Assert.Single(results);
            Assert.Equal("test:1", results[0].Chunk.Id);
            Assert.Equal("value", results[0].Chunk.Metadata["key"]);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}

public class WordPieceTokenizerTests
{
    [Fact]
    public void Encode_ProducesIdsWithSpecialTokens()
    {
        var vocabLines = new[]
        {
            "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello", "world", "##ing", "test"
        };
        var tempPath = Path.Combine(Path.GetTempPath(), $"vocab_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllLines(tempPath, vocabLines);
            var tokenizer = new WordPieceTokenizer(tempPath);

            var (ids, mask) = tokenizer.Encode("hello world", maxLength: 512);

            Assert.Equal(4, ids.Length);
            Assert.Equal(2, ids[0]);
            Assert.Equal(4, ids[1]);
            Assert.Equal(5, ids[2]);
            Assert.Equal(3, ids[^1]);
            Assert.All(mask, m => Assert.Equal(1L, m));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Encode_HandlesUnknownWords()
    {
        var vocabLines = new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello" };
        var tempPath = Path.Combine(Path.GetTempPath(), $"vocab_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllLines(tempPath, vocabLines);
            var tokenizer = new WordPieceTokenizer(tempPath);

            var (ids, _) = tokenizer.Encode("hello xyzqwerty", maxLength: 512);

            Assert.Contains(1L, ids);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
