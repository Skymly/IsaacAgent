using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace IsaacAgent.Rag.Embedding;

public sealed class OnnxEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly InferenceSession _session;
    private readonly WordPieceTokenizer _tokenizer;
    private readonly ILogger<OnnxEmbeddingProvider> _logger;
    private readonly int _dimensions;
    private bool _disposed;

    public OnnxEmbeddingProvider(string modelPath, string vocabPath, ILogger<OnnxEmbeddingProvider> logger)
    {
        _logger = logger;

        if (!File.Exists(modelPath))
            throw new FileNotFoundException("ONNX embedding model not found. Download all-MiniLM-L6-v2 ONNX and place at configured path.", modelPath);
        if (!File.Exists(vocabPath))
            throw new FileNotFoundException("Tokenizer vocab file not found.", vocabPath);

        var options = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        _session = new InferenceSession(modelPath, options);
        _tokenizer = new WordPieceTokenizer(vocabPath);

        _dimensions = 384;
    }

    public string ModelName => "onnx-minilm-l6-v2";

    public int Dimensions => _dimensions;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var batch = EmbedBatchAsync([text], ct);
        return batch.ContinueWith(t => t.Result[0], TaskScheduler.Default);
    }

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        return Task.Run<IReadOnlyList<float[]>>(() =>
        {
            var results = new float[texts.Count][];
            for (var i = 0; i < texts.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                results[i] = EmbedSingle(texts[i]);
            }
            return results;
        }, ct);
    }

    private float[] EmbedSingle(string text)
    {
        var (ids, attentionMask) = _tokenizer.Encode(text, maxLength: 512);
        var tokenTypeIds = new long[ids.Length];

        var inputIds = new DenseTensor<long>(ids, new[] { 1, ids.Length });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });
        var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, tokenTypeIds.Length });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor),
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();

        return MeanPoolAndNormalize(output, attentionMask);
    }

    private static float[] MeanPoolAndNormalize(Tensor<float> tokenEmbeddings, long[] attentionMask)
    {
        var seqLen = tokenEmbeddings.Dimensions[1];
        var dim = tokenEmbeddings.Dimensions[2];
        var pooled = new float[dim];

        var maskSum = 0;
        for (var s = 0; s < seqLen; s++)
        {
            if (attentionMask[s] == 0) continue;
            maskSum++;
            for (var d = 0; d < dim; d++)
                pooled[d] += tokenEmbeddings[0, s, d];
        }

        if (maskSum > 0)
            for (var d = 0; d < dim; d++)
                pooled[d] /= maskSum;

        var norm = 0f;
        for (var d = 0; d < dim; d++)
            norm += pooled[d] * pooled[d];
        norm = MathF.Sqrt(norm);
        if (norm > 0)
            for (var d = 0; d < dim; d++)
                pooled[d] /= norm;

        return pooled;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _session.Dispose();
            _disposed = true;
        }
    }
}
