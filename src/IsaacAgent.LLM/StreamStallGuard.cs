using Microsoft.Extensions.Logging;

namespace IsaacAgent.LLM;

/// <summary>
/// Idle-timeout policy for streaming body reads after response headers arrive.
/// Shared by SSE (OpenAI-compatible) and NDJSON (Ollama) providers.
/// </summary>
internal static class StreamStallGuard
{
    /// <summary>
    /// Creates a linked CTS that cancels after <paramref name="idleTimeout"/> of
    /// inactivity, while still honouring <paramref name="userCt"/>.
    /// </summary>
    public static CancellationTokenSource CreateIdleTimeoutSource(
        CancellationToken userCt,
        TimeSpan idleTimeout)
    {
        var idleCts = CancellationTokenSource.CreateLinkedTokenSource(userCt);
        idleCts.CancelAfter(idleTimeout);
        return idleCts;
    }

    /// <summary>
    /// Reads one line with an idle timeout. Resets <paramref name="idleCts"/> on
    /// success. Throws <see cref="TimeoutException"/> when the idle timer fires
    /// without user cancellation; propagates user cancellation otherwise.
    /// </summary>
    public static async Task<string?> ReadLineOrThrowIfStalledAsync(
        TextReader reader,
        CancellationTokenSource idleCts,
        TimeSpan idleTimeout,
        CancellationToken userCt,
        ILogger logger)
    {
        userCt.ThrowIfCancellationRequested();
        try
        {
            var line = await reader.ReadLineAsync(idleCts.Token);
            idleCts.CancelAfter(idleTimeout);
            return line;
        }
        catch (OperationCanceledException) when (idleCts.IsCancellationRequested && !userCt.IsCancellationRequested)
        {
            logger.LogWarning("Stream read timed out after {Seconds}s with no data", idleTimeout.TotalSeconds);
            throw new TimeoutException(
                $"LLM stream stalled: no data received within {idleTimeout.TotalSeconds:F0}s.");
        }
    }
}
