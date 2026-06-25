using System.Net;
using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.LLM.Providers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class StreamTimeoutTests
{
    /// <summary>
    /// HttpMessageHandler that returns a response with headers but whose
    /// content stream never produces any data (simulates a stalled server).
    /// </summary>
    private sealed class StalledStreamHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var content = new StreamContent(new NeverCompletingStream());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }

    /// <summary>
    /// A stream that never returns data and never completes.
    /// ReadAsync blocks indefinitely until the cancellationToken cancels.
    /// </summary>
    private sealed class NeverCompletingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotImplementedException();
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotImplementedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<int>();
            ct.Register(() => tcs.TrySetCanceled());
            return tcs.Task;
        }
    }

    /// <summary>
    /// HttpMessageHandler that sends one SSE line then stalls forever.
    /// </summary>
    private sealed class OneLineThenStallHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var content = new StreamContent(new OneLineThenStallStream());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }

    private sealed class OneLineThenStallStream : Stream
    {
        private bool _firstReadDone;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotImplementedException();
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotImplementedException();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            if (!_firstReadDone)
            {
                _firstReadDone = true;
                var line = System.Text.Encoding.UTF8.GetBytes("data: {\"choices\":[{\"delta\":{\"content\":\"hi\"}}]}\n");
                var len = Math.Min(line.Length, count);
                Array.Copy(line, 0, buffer, offset, len);
                return len;
            }
            // Stall forever on second read
            var tcs = new TaskCompletionSource<int>();
            ct.Register(() => tcs.TrySetCanceled());
            await tcs.Task;
            return 0; // unreachable
        }
    }

    private static OpenAICompatibleProvider CreateOpenAIProvider(HttpClient http, TimeSpan? timeout = null) =>
        new(http, "test-model", Mock.Of<ILogger<OpenAICompatibleProvider>>(),
            streamReadTimeout: timeout ?? TimeSpan.FromMilliseconds(500));

    private static OllamaProvider CreateOllamaProvider(HttpClient http) =>
        new(http, "test-model", Mock.Of<ILogger<OllamaProvider>>(),
            streamReadTimeout: TimeSpan.FromMilliseconds(500));

    [Fact]
    public async Task OpenAI_StreamStalled_ThrowsTimeoutException()
    {
        using var http = new HttpClient(new StalledStreamHandler()) { BaseAddress = new Uri("http://localhost") };
        var provider = CreateOpenAIProvider(http);

        var request = new ChatRequest { Messages = [ChatMessage.User("test")] };

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var _ in provider.StreamAsync(request)) { }
        });
    }

    [Fact]
    public async Task OpenAI_StreamStallsAfterFirstLine_ThrowsTimeoutException()
    {
        using var http = new HttpClient(new OneLineThenStallHandler()) { BaseAddress = new Uri("http://localhost") };
        var provider = CreateOpenAIProvider(http);

        var request = new ChatRequest { Messages = [ChatMessage.User("test")] };

        // Should receive the first chunk ("hi") then timeout on the stalled second read
        var chunks = new List<string>();
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var c in provider.StreamAsync(request))
                chunks.Add(c.Delta ?? "");
        });

        // Verify we got the first chunk before the timeout
        Assert.Contains("hi", chunks);
    }

    [Fact]
    public async Task Ollama_StreamStalled_ThrowsTimeoutException()
    {
        using var http = new HttpClient(new StalledStreamHandler()) { BaseAddress = new Uri("http://localhost") };
        var provider = CreateOllamaProvider(http);

        var request = new ChatRequest { Messages = [ChatMessage.User("test")] };

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await foreach (var _ in provider.StreamAsync(request)) { }
        });
    }

    [Fact]
    public async Task OpenAI_Stream_UserCancellation_PropagatesNotTimeout()
    {
        using var http = new HttpClient(new StalledStreamHandler()) { BaseAddress = new Uri("http://localhost") };
        // Use a long StreamReadTimeout (5s) so the user's CTS (200ms) always
        // fires first, even on slow CI runners. This tests that user
        // cancellation propagates as OperationCanceledException, not
        // TimeoutException.
        var provider = CreateOpenAIProvider(http, TimeSpan.FromSeconds(5));

        var request = new ChatRequest { Messages = [ChatMessage.User("test")] };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // User cancellation should throw OperationCanceledException (or its
        // subclass TaskCanceledException), not TimeoutException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in provider.StreamAsync(request, cts.Token)) { }
        });
    }
}
