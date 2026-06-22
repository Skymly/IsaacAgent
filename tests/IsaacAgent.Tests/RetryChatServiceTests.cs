using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.LLM;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class RetryChatServiceTests
{
    private static RetryChatService CreateService(IChatService inner, int maxRetries = 3)
    {
        var delays = new[] { TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(30) };
        return new RetryChatService(inner, maxRetries, delays, Mock.Of<ILogger<RetryChatService>>());
    }

    private sealed class ThrowingChatService : IChatService
    {
        private readonly Exception _ex;
        public int CallCount { get; private set; }

        public ThrowingChatService(Exception ex) => _ex = ex;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
        {
            CallCount++;
            throw _ex;
        }

        public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            CallCount++;
            await Task.Yield();
            throw _ex;
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class FailThenSucceedChatService : IChatService
    {
        private readonly int _failCount;
        public int CallCount { get; private set; }

        public FailThenSucceedChatService(int failCount) => _failCount = failCount;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
        {
            CallCount++;
            if (CallCount <= _failCount)
                throw new HttpRequestException("Simulated failure");
            return Task.FromResult(new ChatResponse
            {
                Message = new ChatMessage { Role = "assistant", Content = "recovered" }
            });
        }

        public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            CallCount++;
            if (CallCount <= _failCount)
            {
                await Task.Yield();
                throw new HttpRequestException("Simulated failure");
            }
            await Task.Yield();
            yield return new ChatChunk("recovered", false, -1, null, null, null);
        }
    }

    private sealed class StreamingFailAfterYieldChatService : IChatService
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            CallCount++;
            await Task.Yield();
            yield return new ChatChunk("partial", false, -1, null, null, null);
            throw new HttpRequestException("Failed mid-stream");
        }
    }

    [Fact]
    public async Task CompleteAsync_SucceedsOnFirstAttempt_NoRetry()
    {
        var inner = new FailThenSucceedChatService(failCount: 0);
        var svc = CreateService(inner, maxRetries: 3);

        var result = await svc.CompleteAsync(new ChatRequest { Messages = [] });

        Assert.Equal("recovered", result.Message.Content);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_RetriesOnFailure_ThenSucceeds()
    {
        var inner = new FailThenSucceedChatService(failCount: 2);
        var svc = CreateService(inner, maxRetries: 3);

        var result = await svc.CompleteAsync(new ChatRequest { Messages = [] });

        Assert.Equal("recovered", result.Message.Content);
        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_ExhaustsRetries_ThenThrows()
    {
        var inner = new ThrowingChatService(new HttpRequestException("always fail"));
        var svc = CreateService(inner, maxRetries: 2);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            svc.CompleteAsync(new ChatRequest { Messages = [] }));

        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_DoesNotRetry_OnCancellation()
    {
        var inner = new ThrowingChatService(new OperationCanceledException());
        var svc = CreateService(inner, maxRetries: 3);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            svc.CompleteAsync(new ChatRequest { Messages = [] }, cts.Token));

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task StreamAsync_RetriesBeforeFirstChunk_ThenSucceeds()
    {
        var inner = new FailThenSucceedChatService(failCount: 1);
        var svc = CreateService(inner, maxRetries: 3);

        var chunks = new List<string>();
        await foreach (var chunk in svc.StreamAsync(new ChatRequest { Messages = [] }))
            chunks.Add(chunk.Delta);

        Assert.Contains("recovered", chunks);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task StreamAsync_DoesNotRetry_AfterFirstChunk()
    {
        var inner = new StreamingFailAfterYieldChatService();
        var svc = CreateService(inner, maxRetries: 3);

        var chunks = new List<string>();
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var chunk in svc.StreamAsync(new ChatRequest { Messages = [] }))
                chunks.Add(chunk.Delta);
        });

        Assert.Single(chunks);
        Assert.Equal("partial", chunks[0]);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task StreamAsync_ExhaustsRetries_ThenThrows()
    {
        var inner = new ThrowingChatService(new HttpRequestException("always fail"));
        var svc = CreateService(inner, maxRetries: 2);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in svc.StreamAsync(new ChatRequest { Messages = [] })) { }
        });

        Assert.Equal(3, inner.CallCount);
    }
}
