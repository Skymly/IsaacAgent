using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.LLM.Providers;

public sealed class OpenAICompatibleProvider : IChatService, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAICompatibleProvider> _logger;
    private readonly string _model;
    private bool _disposed;

    /// <summary>
    /// Idle timeout for SSE stream reads. If no data arrives within this
    /// window, the stream is considered stalled and a TimeoutException is
    /// thrown. Defaults to 90s; can be overridden via constructor for testing.
    /// </summary>
    internal TimeSpan StreamReadTimeout { get; } = TimeSpan.FromSeconds(90);

    public OpenAICompatibleProvider(HttpClient http, string model, ILogger<OpenAICompatibleProvider> logger,
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
        using var resp = await _http.PostAsJsonAsync("/v1/chat/completions", payload, ct);
        EnsureSuccessStatusCodeWithDetail(resp);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        var usage = doc.RootElement.GetProperty("usage");

        var message = new ChatMessage
        {
            Role = choice.GetProperty("role").GetString() ?? "assistant",
            Content = choice.GetProperty("content").GetString() ?? "",
            ToolCalls = ParseToolCalls(choice)
        };

        return new ChatResponse
        {
            Message = message,
            InputTokens = usage.GetProperty("prompt_tokens").GetInt32(),
            OutputTokens = usage.GetProperty("completion_tokens").GetInt32()
        };
    }

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = BuildPayload(request, stream: true);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        EnsureSuccessStatusCodeWithDetail(resp);

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // SSE streaming: HttpClient.Timeout may not reliably cancel a stalled
        // stream after headers are received. Use a linked CTS that resets on
        // each successful line read so a hung server doesn't leave the UI
        // spinning indefinitely.
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
                throw new TimeoutException($"LLM stream stalled: no data received within {StreamReadTimeout.TotalSeconds:F0}s.");
            }
            // Reset the idle timeout for the next line
            timeoutCts.CancelAfter(StreamReadTimeout);

            if (line is null) break; // EOF
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(data);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Skipping malformed JSON line in stream: {Line}", data);
                continue;
            }
            using (doc)
            {
                var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");

                var content = delta.TryGetProperty("content", out var c) ? c.GetString() : null;

                if (delta.TryGetProperty("tool_calls", out var tc) && tc.GetArrayLength() > 0)
                {
                    // A single delta chunk may carry multiple tool calls (some OpenAI-compatible
                    // endpoints batch them). Emit one ChatChunk per tool call so AgentSession's
                    // index-keyed accumulator can capture all of them.
                    foreach (var item in tc.EnumerateArray())
                    {
                        var idx = item.TryGetProperty("index", out var idxEl) ? idxEl.GetInt32() : 0;
                        var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                        string? name = null;
                        string? args = null;
                        if (item.TryGetProperty("function", out var fn))
                        {
                            name = fn.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                            args = fn.TryGetProperty("arguments", out var argsEl) ? argsEl.GetString() : null;
                        }
                        yield return new ChatChunk("", true, idx, id, name, args);
                    }
                    continue;
                }

                if (content is not null)
                    yield return new ChatChunk(content, false, -1, null, null, null);
            }
        }
    }

    private object BuildPayload(ChatRequest request, bool stream)
    {
        var messages = request.Messages.Select(m =>
        {
            object? toolCalls = null;
            if (m.ToolCalls.Count > 0)
            {
                toolCalls = m.ToolCalls.Select(tc => new
                {
                    id = tc.Id,
                    type = "function",
                    function = new { name = tc.Name, arguments = tc.Arguments }
                }).ToArray();
            }
            var msg = new Dictionary<string, object?>
            {
                ["role"] = m.Role,
                ["content"] = m.Content,
            };
            if (toolCalls is not null) msg["tool_calls"] = toolCalls;
            if (m.ToolCallId is not null) msg["tool_call_id"] = m.ToolCallId;
            return (object)msg;
        }).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model ?? _model,
            ["messages"] = messages,
            ["temperature"] = request.Temperature,
            ["max_tokens"] = request.MaxTokens,
            ["stream"] = stream,
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

    private static List<ToolCall> ParseToolCalls(JsonElement choice)
    {
        if (!choice.TryGetProperty("tool_calls", out var tc)) return [];
        var calls = new List<ToolCall>();
        foreach (var item in tc.EnumerateArray())
        {
            calls.Add(new ToolCall
            {
                Id = item.GetProperty("id").GetString() ?? "",
                Name = item.GetProperty("function").GetProperty("name").GetString() ?? "",
                Arguments = item.GetProperty("function").GetProperty("arguments").GetString() ?? "{}"
            });
        }
        return calls;
    }

    /// <summary>
    /// Throws an <see cref="HttpRequestException"/> with a descriptive message
    /// for common non-success status codes, falling back to
    /// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> for others.
    /// </summary>
    private void EnsureSuccessStatusCodeWithDetail(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;

        var status = resp.StatusCode;
        if (status == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException(
                $"Rate limited by LLM provider (429 Too ManyRequests). Request will be retried after backoff.",
                null, status);
        }

        if (status == System.Net.HttpStatusCode.Unauthorized ||
            status == System.Net.HttpStatusCode.Forbidden)
        {
            throw new HttpRequestException(
                $"Authentication failed ({(int)status} {status}). Check API key and permissions.",
                null, status);
        }

        resp.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _http.Dispose();
            _disposed = true;
        }
    }
}
