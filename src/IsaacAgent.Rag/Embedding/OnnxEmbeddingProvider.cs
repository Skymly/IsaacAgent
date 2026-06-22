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
    private int _dimensions;
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

        // Infer dimensions from the model's output metadata.
        // Fallback to 384 (all-MiniLM-L6-v2 default) if metadata is unavailable.
        _dimensions = InferDimensions();
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
            if (texts.Count == 0) return [];

            // Tokenize all texts, then pad to the max sequence length in this batch
            // so we can run a single forward pass through the ONNX model.
            const int maxSeqLen = 512;
            var encoded = new (long[] Ids, long[] Mask)[texts.Count];
            var batchSeqLen = 0;
            for (var i = 0; i < texts.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (ids, mask) = _tokenizer.Encode(texts[i], maxLength: maxSeqLen);
                encoded[i] = (ids, mask);
                if (ids.Length > batchSeqLen) batchSeqLen = ids.Length;
            }

            // Build padded tensors: [batch, batchSeqLen]
            var inputIds = new DenseTensor<long>(new[] { texts.Count, batchSeqLen });
            var attentionMaskTensor = new DenseTensor<long>(new[] { texts.Count, batchSeqLen });
            var tokenTypeIdsTensor = new DenseTensor<long>(new[] { texts.Count, batchSeqLen });

            for (var i = 0; i < texts.Count; i++)
            {
                var (ids, mask) = encoded[i];
                for (var j = 0; j < ids.Length; j++)
                {
                    inputIds[i, j] = ids[j];
                    attentionMaskTensor[i, j] = mask[j];
                    // tokenTypeIds stay 0 (single-sentence model)
                }
                // Remaining positions are already 0 (padding)
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor),
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            // Update dimensions from actual output if not yet set
            if (_dimensions == 0 && output.Dimensions.Length >= 3)
                _dimensions = output.Dimensions[2];

            // Pool and normalize each sequence in the batch
            var embeddings = new float[texts.Count][];
            for (var i = 0; i < texts.Count; i++)
            {
                embeddings[i] = MeanPoolAndNormalize(output, encoded[i].Mask, i);
            }
            return embeddings;
        }, ct);
    }

    private int InferDimensions()
    {
        // Try to read from model output metadata
        try
        {
            var output = _session.OutputMetadata.FirstOrDefault();
            if (output.Value?.Dimensions is { Length: >= 3 } dims && dims[2] > 0)
                return dims[2];
        }
        catch { }

        // Fallback: run a dummy forward pass to infer from actual output shape
        try
        {
            var dummyIds = new DenseTensor<long>(new[] { 1, 1 });
            var dummyMask = new DenseTensor<long>(new[] { 1, 1 });
            dummyMask[0, 0] = 1;
            var dummyType = new DenseTensor<long>(new[] { 1, 1 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", dummyIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", dummyMask),
                NamedOnnxValue.CreateFromTensor("token_type_ids", dummyType),
            };
            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();
            if (output.Dimensions.Length >= 3)
                return output.Dimensions[2];
        }
        catch { }

        // Final fallback: all-MiniLM-L6-v2 default
        return 384;
    }

    private static float[] MeanPoolAndNormalize(Tensor<float> tokenEmbeddings, long[] attentionMask, int batchIndex)
    {
        var seqLen = tokenEmbeddings.Dimensions[1];
        var dim = tokenEmbeddings.Dimensions[2];
        var pooled = new float[dim];

        var maskSum = 0;
        for (var s = 0; s < seqLen; s++)
        {
            if (s >= attentionMask.Length || attentionMask[s] == 0) continue;
            maskSum++;
            for (var d = 0; d < dim; d++)
                pooled[d] += tokenEmbeddings[batchIndex, s, d];
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
