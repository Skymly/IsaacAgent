using System.Text;
using IsaacAgent.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IsaacAgent.Tests;

public class StreamStallGuardTests
{
    private static readonly ILogger Logger = NullLogger.Instance;

    [Fact]
    public async Task ReadLine_ReturnsLine_AndResetsIdleTimeout()
    {
        using var reader = new StringReader("hello\n");
        using var idleCts = StreamStallGuard.CreateIdleTimeoutSource(
            CancellationToken.None, TimeSpan.FromSeconds(5));

        var line = await StreamStallGuard.ReadLineOrThrowIfStalledAsync(
            reader, idleCts, TimeSpan.FromSeconds(5), CancellationToken.None, Logger);

        Assert.Equal("hello", line);
    }

    [Fact]
    public async Task ReadLine_ReturnsNull_AtEof()
    {
        using var reader = new StringReader("");
        using var idleCts = StreamStallGuard.CreateIdleTimeoutSource(
            CancellationToken.None, TimeSpan.FromSeconds(5));

        var line = await StreamStallGuard.ReadLineOrThrowIfStalledAsync(
            reader, idleCts, TimeSpan.FromSeconds(5), CancellationToken.None, Logger);

        Assert.Null(line);
    }

    [Fact]
    public async Task ReadLine_ThrowsTimeoutException_WhenIdleTimeoutFires()
    {
        await using var stream = new NeverCompletingStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 16, leaveOpen: true);
        var idleTimeout = TimeSpan.FromMilliseconds(200);
        using var idleCts = StreamStallGuard.CreateIdleTimeoutSource(CancellationToken.None, idleTimeout);

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            StreamStallGuard.ReadLineOrThrowIfStalledAsync(
                reader, idleCts, idleTimeout, CancellationToken.None, Logger));

        Assert.Contains("stalled", ex.Message);
        Assert.Contains("no data received", ex.Message);
    }

    [Fact]
    public async Task ReadLine_PropagatesUserCancellation_NotTimeout()
    {
        await using var stream = new NeverCompletingStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 16, leaveOpen: true);
        using var userCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var idleCts = StreamStallGuard.CreateIdleTimeoutSource(userCts.Token, TimeSpan.FromSeconds(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            StreamStallGuard.ReadLineOrThrowIfStalledAsync(
                reader, idleCts, TimeSpan.FromSeconds(5), userCts.Token, Logger));
    }

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
}
