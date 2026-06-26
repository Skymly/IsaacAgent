using IsaacAgent.Core.Models;

namespace IsaacAgent.Core.Services;

/// <summary>
/// Abstraction over a chat completion provider (e.g. OpenAI-compatible, Ollama).
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Streams chat completion chunks as an async enumerable.
    /// </summary>
    IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// Completes a chat request and returns the full response.
    /// </summary>
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default);
}

/// <summary>
/// Parameters for a chat completion request.
/// </summary>
public sealed class ChatRequest
{
    /// <summary>Conversation messages sent to the model.</summary>
    public required List<ChatMessage> Messages { get; init; }

    /// <summary>Tools available to the model during completion.</summary>
    public List<ToolDefinition> Tools { get; init; } = [];

    /// <summary>Model identifier to use; null falls back to provider default.</summary>
    public string? Model { get; init; }

    /// <summary>Sampling temperature (0-2).</summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>Maximum tokens to generate.</summary>
    public int MaxTokens { get; init; } = 4096;

    /// <summary>Whether to stream the response.</summary>
    public bool Stream { get; init; } = false;
}

/// <summary>
/// The full response from a non-streaming chat completion.
/// </summary>
public sealed class ChatResponse
{
    /// <summary>The assistant's reply message.</summary>
    public required ChatMessage Message { get; init; }

    /// <summary>Number of input tokens consumed.</summary>
    public int InputTokens { get; init; }

    /// <summary>Number of output tokens generated.</summary>
    public int OutputTokens { get; init; }
}

/// <summary>
/// A chunk from a streaming chat response. Text deltas arrive as Delta.
/// Tool call fragments arrive with IsToolCall = true.
/// </summary>
public readonly record struct ChatChunk(
    string Delta,
    bool IsToolCall,
    int ToolCallIndex,
    string? ToolCallId,
    string? ToolCallName,
    string? ToolCallArguments);
