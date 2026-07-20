using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Embedding;
using IsaacAgent.Rag.Indexing;
using IsaacAgent.Rag.Retrieval;
using IsaacAgent.Rag.Store;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Embedding apply seam (#12): switch provider (any dimensions), invalidate knowledge index, rebuild.
/// </summary>
public class EmbeddingApplyTests
{
    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        public required string ModelName { get; init; }
        public required int Dimensions { get; init; }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            var v = new float[Dimensions];
            v[0] = 1f;
            return Task.FromResult(v);
        }

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(
                texts.Select(_ =>
                {
                    var v = new float[Dimensions];
                    v[0] = 1f;
                    return v;
                }).ToList());
    }

    private static (EmbeddingApply Apply, EmbeddingProviderProxy Proxy, Retriever Retriever, InMemoryVectorStore Store, string IndexPath, string TempDir)
        CreateSut(IEmbeddingProvider initial)
    {
        var store = new InMemoryVectorStore();
        var proxy = new EmbeddingProviderProxy(initial);
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_embed_apply_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var indexPath = Path.Combine(tempDir, "index.bin");
        var builder = new IndexBuilder(proxy, store, tempDir, Mock.Of<ILogger<IndexBuilder>>());
        var retriever = new Retriever(proxy, store, builder, indexPath, Mock.Of<ILogger<Retriever>>());
        var apply = new EmbeddingApply(proxy, retriever, store, indexPath);
        return (apply, proxy, retriever, store, indexPath, tempDir);
    }

    [Fact]
    public async Task ApplyAsync_CrossDimension_RebuildsSearchableIndex()
    {
        var initial = new StubEmbeddingProvider { ModelName = "dim-3", Dimensions = 3 };
        var (apply, proxy, retriever, store, indexPath, tempDir) = CreateSut(initial);

        try
        {
            await retriever.RebuildIndexAsync();
            Assert.True(retriever.IsReady);
            Assert.Equal(3, store.Dimensions);
            Assert.True(store.Count > 0);

            var next = new StubEmbeddingProvider { ModelName = "dim-5", Dimensions = 5 };
            await apply.ApplyAsync(next);

            Assert.Equal("dim-5", proxy.ModelName);
            Assert.Equal(5, proxy.Dimensions);
            Assert.True(retriever.IsReady);
            Assert.Equal(5, store.Dimensions);
            Assert.Equal("dim-5", store.ModelName);
            Assert.True(store.Count > 0);

            var hits = await retriever.SearchAsync("callback");
            Assert.NotEmpty(hits);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_SameDimension_StillRebuilds()
    {
        var initial = new StubEmbeddingProvider { ModelName = "model-a", Dimensions = 3 };
        var (apply, proxy, retriever, store, indexPath, tempDir) = CreateSut(initial);

        try
        {
            await retriever.RebuildIndexAsync();
            var builtAtBefore = store.BuiltAt;
            Assert.True(store.Count > 0);

            await Task.Delay(20); // ensure BuiltAt can advance

            var next = new StubEmbeddingProvider { ModelName = "model-b", Dimensions = 3 };
            await apply.ApplyAsync(next);

            Assert.Equal("model-b", store.ModelName);
            Assert.Equal(3, store.Dimensions);
            Assert.True(retriever.IsReady);
            Assert.True(store.BuiltAt > builtAtBefore);
            Assert.NotEmpty(await retriever.SearchAsync("item"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyAsync_AfterDimensionChange_OldVectorsNotValidForSearch()
    {
        var initial = new StubEmbeddingProvider { ModelName = "dim-3", Dimensions = 3 };
        var (apply, proxy, retriever, store, _, tempDir) = CreateSut(initial);

        try
        {
            await retriever.RebuildIndexAsync();
            Assert.Equal(3, store.Dimensions);

            await apply.ApplyAsync(new StubEmbeddingProvider { ModelName = "dim-8", Dimensions = 8 });

            Assert.Equal(8, store.Dimensions);
            Assert.All(store.ExportIndex().Entries, e => Assert.Equal(8, e.Vector.Length));
            Assert.DoesNotContain(store.ExportIndex().Entries, e => e.Vector.Length == 3);

            var hits = await retriever.SearchAsync("callback");
            Assert.NotEmpty(hits);
            Assert.True(retriever.IsReady);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }
}
�
