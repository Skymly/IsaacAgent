using System.Runtime.CompilerServices;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.LLM;

public sealed class RetryChatService : IChatService
{
    private readonly IChatService _inner;
    private readonly int _maxRetries;
    private readonly TimeSpan[] _retryDelays;
    private readonly ILogger _logger;

    public RetryChatService(IChatService inner, int maxRetries, TimeSpan[] retryDelays, ILogger logger)
    {
        _inner = inner;
        _maxRetries = maxRetries;
        _retryDelays = retryDelays;
        _logger = logger;
    }

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await _inner.CompleteAsync(request, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                var delay = _retryDelays[Math.Min(attempt, _retryDelays.Length - 1)];
                _logger.LogWarning(ex, "CompleteAsync attempt {Attempt} failed, retrying in {Delay}s", attempt + 1, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            var retry = false;
            var hasYielded = false;
            IAsyncEnumerable<ChatChunk> source = _inner.StreamAsync(request, ct);

            IAsyncEnumerator<ChatChunk> enumerator = source.GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await enumerator.MoveNextAsync();
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        yield break;
                    }
                    catch (Exception ex) when (attempt < _maxRetries && !hasYielded)
                    {
                        var delay = _retryDelays[Math.Min(attempt, _retryDelays.Length - 1)];
                        _logger.LogWarning(ex, "StreamAsync attempt {Attempt} failed, retrying in {Delay}s", attempt + 1, delay.TotalSeconds);
                        await Task.Delay(delay, ct);
                        retry = true;
                        break;
                    }

                    if (!hasNext)
                        yield break;

                    hasYielded = true;
                    yield return enumerator.Current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            if (!retry)
                yield break;
        }
    }
}
