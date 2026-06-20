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
        var payload = new
        {
            model = request.Model ?? _model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = false,
            options = new { temperature = request.Temperature }
        };

        using var resp = await _http.PostAsJsonAsync("/api/chat", payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var message = doc.RootElement.GetProperty("message");

        return new ChatResponse
        {
            Message = new ChatMessage
            {
                Role = message.GetProperty("role").GetString() ?? "assistant",
                Content = message.GetProperty("content").GetString() ?? ""
            },
            InputTokens = 0,
            OutputTokens = 0
        };
    }

    public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            model = request.Model ?? _model,
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            stream = true,
            options = new { temperature = request.Temperature }
        };

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
            if (doc.RootElement.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                var text = content.GetString();
                if (!string.IsNullOrEmpty(text))
                    yield return new ChatChunk(text, false, null, null);
            }
        }
    }
}
