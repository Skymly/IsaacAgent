using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.LLM;

public sealed class ChatServiceProxy : IChatService
{
    private volatile IChatService _inner;

    public ChatServiceProxy(IChatService initial)
    {
        _inner = initial;
    }

    public void Replace(IChatService newService)
    {
        _inner = newService;
    }

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
        => _inner.CompleteAsync(request, ct);

    public IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, CancellationToken ct = default)
        => _inner.StreamAsync(request, ct);
}
