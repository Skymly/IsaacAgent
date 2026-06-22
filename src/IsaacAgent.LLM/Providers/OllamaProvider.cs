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

    public OllamaProvider(HttpClient http, string model, ILogger<OllamaProvider> logger)
    {
        _http = http;
        _model = model;
        _logger = logger;
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

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null) continue;

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
        var messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Content,
            tool_calls = m.ToolCalls.Count > 0 ? m.ToolCalls.Select(tc => new
            {
                function = new { name = tc.Name, arguments = JsonSerializer.Deserialize<JsonElement>(tc.Arguments) }
            }) : null,
            tool_call_id = m.ToolCallId
        }).ToArray();

        var tools = request.Tools.Count > 0 ? request.Tools.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            }
        }).ToArray() : null;

        return new
        {
            model = request.Model ?? _model,
            messages,
            stream,
            tools,
            options = new { temperature = request.Temperature }
        };
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
