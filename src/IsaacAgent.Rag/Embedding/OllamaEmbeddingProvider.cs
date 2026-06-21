using System.Net.Http.Json;
using System.Text.Json;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Rag.Embedding;

public sealed class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;
    private int? _dimensions;

    public OllamaEmbeddingProvider(HttpClient http, string model, ILogger<OllamaEmbeddingProvider> logger)
    {
        _http = http;
        _model = model;
        _logger = logger;
    }

    public string ModelName => _model;

    public int Dimensions => _dimensions ?? 768;

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var batch = await EmbedBatchAsync([text], ct);
        return batch[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new float[texts.Count][];
        for (var i = 0; i < texts.Count; i++)
        {
            var payload = new { model = _model, prompt = texts[i] };
            using var resp = await _http.PostAsJsonAsync("/api/embeddings", payload, ct);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var embedding = doc.RootElement.GetProperty("embedding");
            var arr = new float[embedding.GetArrayLength()];
            var j = 0;
            foreach (var v in embedding.EnumerateArray())
                arr[j++] = v.GetSingle();
            results[i] = arr;
            _dimensions ??= arr.Length;
        }
        return results;
    }
}
