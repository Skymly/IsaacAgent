using IsaacAgent.Core.Models;

namespace IsaacAgent.Core.Services;

public interface IChatService
{
    IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, CancellationToken ct = default);
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default);
}

public sealed class ChatRequest
{
    public required List<ChatMessage> Messages { get; init; }
    public List<ToolDefinition> Tools { get; init; } = [];
    public string? Model { get; init; }
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 4096;
    public bool Stream { get; init; } = false;
}

public sealed class ChatResponse
{
    public required ChatMessage Message { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
}

public readonly record struct ChatChunk(string Delta, bool IsToolCall, string? ToolCallId, string? ToolCallName);
