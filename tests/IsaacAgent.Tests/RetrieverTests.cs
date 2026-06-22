using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Indexing;
using IsaacAgent.Rag.Retrieval;
using IsaacAgent.Rag.Store;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class RetrieverTests
{
    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        public string ModelName { get; set; } = "test-model";
        public int Dimensions { get; set; } = 3;

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[] { 1f, 0f, 0f });

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[] { 1f, 0f, 0f }).ToList());
    }

    private static Retriever CreateRetriever(
        IEmbeddingProvider embedding,
        InMemoryVectorStore store,
        IndexBuilder builder,
        string indexPath)
    {
        return new Retriever(
            embedding,
            store,
            builder,
            indexPath,
            Mock.Of<ILogger<Retriever>>());
    }

    private static IndexBuilder CreateBuilder(IEmbeddingProvider embedding, InMemoryVectorStore store, out string tempDir)
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"isaac_retriever_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return new IndexBuilder(embedding, store, tempDir, Mock.Of<ILogger<IndexBuilder>>());
    }

    [Fact]
    public async Task ResetReady_SetsIsReadyToFalse()
    {
        var embedding = new StubEmbeddingProvider();
        var store = new InMemoryVectorStore();
        var indexPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.bin");
        var builder = CreateBuilder(embedding, store, out var tempDir);

        try
        {
            var retriever = CreateRetriever(embedding, store, builder, indexPath);

            // Build index to set IsReady = true
            await retriever.RebuildIndexAsync();
            Assert.True(retriever.IsReady);

            retriever.ResetReady();
            Assert.False(retriever.IsReady);
        }
        finally
        {
            if (File.Exists(indexPath)) File.Delete(indexPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task EnsureIndexAsync_AfterReset_RebuildsWhenModelMismatch()
    {
        var embedding1 = new StubEmbeddingProvider { ModelName = "model-a", Dimensions = 3 };
        var store = new InMemoryVectorStore();
        var indexPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.bin");
        var builder1 = CreateBuilder(embedding1, store, out var tempDir1);

        try
        {
            var retriever = CreateRetriever(embedding1, store, builder1, indexPath);

            // Build initial index
            await retriever.RebuildIndexAsync();
            Assert.True(retriever.IsReady);
            Assert.Equal("model-a", store.ModelName);

            // Swap to a different model — simulates hot-reload
            var embedding2 = new StubEmbeddingProvider { ModelName = "model-b", Dimensions = 3 };
            var builder2 = CreateBuilder(embedding2, store, out var tempDir2);
            var retriever2 = CreateRetriever(embedding2, store, builder2, indexPath);

            // ResetReady then EnsureIndexAsync should detect mismatch and rebuild
            retriever2.ResetReady();
            Assert.False(retriever2.IsReady);

            await retriever2.EnsureIndexAsync();
            Assert.True(retriever2.IsReady);
            Assert.Equal("model-b", store.ModelName);

            if (Directory.Exists(tempDir2)) Directory.Delete(tempDir2, true);
        }
        finally
        {
            if (File.Exists(indexPath)) File.Delete(indexPath);
            if (Directory.Exists(tempDir1)) Directory.Delete(tempDir1, true);
        }
    }

    [Fact]
    public async Task EnsureIndexAsync_AfterReset_LoadsWhenModelMatches()
    {
        var embedding = new StubEmbeddingProvider { ModelName = "same-model", Dimensions = 3 };
        var store = new InMemoryVectorStore();
        var indexPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.bin");
        var builder = CreateBuilder(embedding, store, out var tempDir);

        try
        {
            var retriever = CreateRetriever(embedding, store, builder, indexPath);

            // Build and save index
            await retriever.RebuildIndexAsync();
            Assert.True(retriever.IsReady);
            Assert.True(store.Count > 0);

            var initialCount = store.Count;

            // Reset and re-ensure with same model — should load from disk, not rebuild
            retriever.ResetReady();
            await retriever.EnsureIndexAsync();

            Assert.True(retriever.IsReady);
            Assert.Equal(initialCount, store.Count);
        }
        finally
        {
            if (File.Exists(indexPath)) File.Delete(indexPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
