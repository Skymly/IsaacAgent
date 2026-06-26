using System.Net.Http.Json;
using System.Text.Json;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Rag.Embedding;

public sealed class OllamaEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    /// <summary>Default embedding dimension for nomic-embed-text when model metadata is unavailable.</summary>
    private const int DefaultEmbeddingDimensions = 768;

    /// <summary>Maximum input text length in characters to avoid exceeding model context.</summary>
    private const int MaxInputChars = 2000;

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;
    private int? _dimensions;
    private bool _disposed;

    public OllamaEmbeddingProvider(HttpClient http, string model, ILogger<OllamaEmbeddingProvider> logger)
    {
        _http = http;
        _model = model;
        _logger = logger;
    }

    public string ModelName => _model;

    public int Dimensions => _dimensions ?? DefaultEmbeddingDimensions;

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var batch = await EmbedBatchAsync([text], ct);
        return batch[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new float[texts.Count][];
        const int maxConcurrency = 8;
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = new Task[texts.Count];

        for (var i = 0; i < texts.Count; i++)
        {
            var idx = i;
            var text = texts[idx];
            // Truncate to avoid exceeding model context length.
            // nomic-embed-text has 8192 token context; ~2000 chars is safe for mixed content.
            if (text.Length > MaxInputChars)
                text = text[..MaxInputChars];

            tasks[idx] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var payload = new { model = _model, prompt = text };
                    using var resp = await _http.PostAsJsonAsync("/api/embeddings", payload, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync(ct);
                        throw new HttpRequestException(
                            $"Ollama embedding failed ({resp.StatusCode}) for text of length {text.Length}: {body}");
                    }

                    using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                    var embedding = doc.RootElement.GetProperty("embedding");
                    var arr = new float[embedding.GetArrayLength()];
                    var j = 0;
                    foreach (var v in embedding.EnumerateArray())
                        arr[j++] = v.GetSingle();
                    results[idx] = arr;
                    _dimensions ??= arr.Length;
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct);
        }

        await Task.WhenAll(tasks);
        return results;
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
