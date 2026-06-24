using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Embedding;
using Xunit;

namespace IsaacAgent.Tests;

public class EmbeddingProviderProxyTests
{
    private sealed class DisposableEmbeddingProvider : IEmbeddingProvider, IDisposable
    {
        public string ModelName => "disposable-model";
        public int Dimensions => 4;
        public bool IsDisposed { get; private set; }

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[] { 1f, 0f, 0f, 0f });

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[] { 1f, 0f, 0f, 0f }).ToList());

        public void Dispose() => IsDisposed = true;
    }

    private sealed class PlainEmbeddingProvider : IEmbeddingProvider
    {
        public string ModelName => "plain-model";
        public int Dimensions => 2;

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[] { 1f, 0f });

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[] { 1f, 0f }).ToList());
    }

    [Fact]
    public void Replace_DisposesOldDisposableInner()
    {
        var old = new DisposableEmbeddingProvider();
        var proxy = new EmbeddingProviderProxy(old);
        Assert.False(old.IsDisposed);

        proxy.Replace(new PlainEmbeddingProvider());

        Assert.True(old.IsDisposed);
        Assert.Equal("plain-model", proxy.ModelName);
    }

    [Fact]
    public void Replace_WithNonDisposableInner_DoesNotThrow()
    {
        var proxy = new EmbeddingProviderProxy(new PlainEmbeddingProvider());
        var replacement = new PlainEmbeddingProvider { };
        // Should not throw even though inner is not IDisposable
        proxy.Replace(replacement);
        Assert.Equal("plain-model", proxy.ModelName);
    }

    [Fact]
    public void Dispose_DisposesInner()
    {
        var inner = new DisposableEmbeddingProvider();
        var proxy = new EmbeddingProviderProxy(inner);

        proxy.Dispose();

        Assert.True(inner.IsDisposed);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var inner = new DisposableEmbeddingProvider();
        var proxy = new EmbeddingProviderProxy(inner);

        proxy.Dispose();
        proxy.Dispose(); // should not throw

        Assert.True(inner.IsDisposed);
    }

    [Fact]
    public async Task DelegatesToCurrentInner()
    {
        var first = new PlainEmbeddingProvider();
        var proxy = new EmbeddingProviderProxy(first);

        var result = await proxy.EmbedAsync("test");
        Assert.Equal(2, result.Length);

        proxy.Replace(new DisposableEmbeddingProvider());
        var result2 = await proxy.EmbedAsync("test");
        Assert.Equal(4, result2.Length);
        Assert.Equal(4, proxy.Dimensions);
    }
}
