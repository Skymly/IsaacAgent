using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.LLM.Providers;

public sealed class OllamaProvider : IChatService
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<OllamaProvider> _logger;

    internal TimeSpan StreamReadTimeout { get; } = TimeSpan.FromSeconds(90);

    public OllamaProvider(HttpClient http, string model, ILogger<OllamaProvider> logger,
        TimeSpan? streamReadTimeout = null)
    {
        _http = http;
        _model = model;
        _logger = logger;
        if (streamReadTimeout is { } t) StreamReadTimeout = t;
    }

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        var payload = BuildPayload(request, stream: false);

        using var resp = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var message = doc.RootElement.GetProperty("message");

        var content = message.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
        var toolCalls = ParseToolCalls(message);

        var chatMessage = new ChatMessage
        {
            Role = message.GetProperty("role").GetString() ?? "assistant",
            Content = content,
            ToolCalls = toolCalls
        };

        var inputTokens = 0;
        var outputTokens = 0;
        if (doc.RootElement.TryGetProperty("prompt_eval_count", out var pe))
            inputTokens = pe.GetInt32();
        if (doc.RootElement.TryGetProperty("eval_count", out var ec))
            outputTokens = ec.GetInt32();

        return new ChatResponse
        {
            Message = chatMessage,
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
    }

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = BuildPayload(request, stream: true);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(payload)
        };
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // NDJSON streaming: same stall protection as OpenAI-compatible provider.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(StreamReadTimeout);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            string? line;
            try
            {
                line = await reader.ReadLineAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                _logger.LogWarning("Stream read timed out after {Seconds}s with no data", StreamReadTimeout.TotalSeconds);
                throw new TimeoutException($"Ollama stream stalled: no data received within {StreamReadTimeout.TotalSeconds:F0}s.");
            }
            timeoutCts.CancelAfter(StreamReadTimeout);

            if (line is null) break; // EOF

            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("message", out var msg))
            {
                var text = msg.TryGetProperty("content", out var content) ? content.GetString() : null;
                if (!string.IsNullOrEmpty(text))
                    yield return new ChatChunk(text, false, -1, null, null, null);

                if (msg.TryGetProperty("tool_calls", out var tc) && tc.GetArrayLength() > 0)
                {
                    for (var i = 0; i < tc.GetArrayLength(); i++)
                    {
                        var item = tc[i];
                        if (item.TryGetProperty("function", out var fn))
                        {
                            var name = fn.TryGetProperty("name", out var n) ? n.GetString() : null;
                            var args = fn.TryGetProperty("arguments", out var a) ? a.GetRawText() : null;
                            yield return new ChatChunk("", true, i, null, name, args);
                        }
                    }
                }
            }
        }
    }

    private object BuildPayload(ChatRequest request, bool stream)
    {
        var messages = request.Messages.Select(m =>
        {
            var msg = new Dictionary<string, object?>
            {
                ["role"] = m.Role,
                ["content"] = m.Content,
            };
            if (m.ToolCalls.Count > 0)
            {
                msg["tool_calls"] = m.ToolCalls.Select(tc =>
                {
                    // Ollama expects arguments as a JSON object, not a string.
                    // If the arguments string is empty or invalid JSON, fall back
                    // to an empty object to avoid deserialization exceptions.
                    JsonElement argsJson;
                    try
                    {
                        argsJson = string.IsNullOrWhiteSpace(tc.Arguments)
                            ? JsonSerializer.Deserialize<JsonElement>("{}")!
                            : JsonSerializer.Deserialize<JsonElement>(tc.Arguments);
                    }
                    catch (JsonException)
                    {
                        argsJson = JsonSerializer.Deserialize<JsonElement>("{}")!;
                    }
                    return (object)new
                    {
                        function = new { name = tc.Name, arguments = argsJson }
                    };
                }).ToArray();
            }
            if (m.ToolCallId is not null)
                msg["tool_call_id"] = m.ToolCallId;
            return (object)msg;
        }).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model ?? _model,
            ["messages"] = messages,
            ["stream"] = stream,
            ["options"] = new { temperature = request.Temperature }
        };

        if (request.Tools.Count > 0)
        {
            payload["tools"] = request.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                }
            }).ToArray();
        }

        return payload;
    }

    private static List<ToolCall> ParseToolCalls(JsonElement message)
    {
        if (!message.TryGetProperty("tool_calls", out var tc)) return [];
        var calls = new List<ToolCall>();
        foreach (var item in tc.EnumerateArray())
        {
            var fn = item.GetProperty("function");
            var name = fn.GetProperty("name").GetString() ?? "";
            var args = fn.TryGetProperty("arguments", out var a) ? a.GetRawText() : "{}";
            var id = (item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null) ?? $"call_{Guid.NewGuid():N}";

            calls.Add(new ToolCall
            {
                Id = id,
                Name = name,
                Arguments = args
            });
        }
        return calls;
    }
}
