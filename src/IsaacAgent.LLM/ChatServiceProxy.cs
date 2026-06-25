using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.LLM;

public sealed class ChatServiceProxy : IChatService, IDisposable
{
    private IChatService _inner;
    private volatile bool _disposed;

    public ChatServiceProxy(IChatService initial)
    {
        _inner = initial;
    }

    public void Replace(IChatService newService)
    {
        var old = System.Threading.Interlocked.Exchange(ref _inner, newService);
        if (old is IDisposable disposable)
            disposable.Dispose();
    }

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
        => _inner.CompleteAsync(request, ct);

    public IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, CancellationToken ct = default)
        => _inner.StreamAsync(request, ct);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_inner is IDisposable disposable)
            disposable.Dispose();
    }
}
