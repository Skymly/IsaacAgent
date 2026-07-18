using IsaacAgent.Rag.Embedding;
using Microsoft.Extensions.Logging.Abstractions;

namespace IsaacAgent.App.Services;

/// <summary>
/// Headless check used by <c>IsaacAgent.exe --verify-onnx</c> and the Nuke
/// <c>PublishVerify</c> target. Confirms bundled assets resolve and the ONNX
/// session can embed one string (covers single-file AppData extraction).
/// </summary>
internal static class OnnxPublishVerifier
{
    private const int ExpectedDimensions = 384;

    public static int Run()
    {
        try
        {
            var modelPath = DefaultOnnxAssets.ResolveModelPath(null);
            var vocabPath = DefaultOnnxAssets.ResolveVocabPath(null);

            if (!File.Exists(modelPath))
            {
                Console.Error.WriteLine($"ONNX model missing: {modelPath}");
                return 1;
            }

            if (!File.Exists(vocabPath))
            {
                Console.Error.WriteLine($"ONNX vocab missing: {vocabPath}");
                return 1;
            }

            using var provider = new OnnxEmbeddingProvider(
                modelPath,
                vocabPath,
                NullLogger<OnnxEmbeddingProvider>.Instance);

            if (provider.Dimensions != ExpectedDimensions)
            {
                Console.Error.WriteLine(
                    $"Unexpected embedding dimensions: {provider.Dimensions} (expected {ExpectedDimensions})");
                return 2;
            }

            var vector = provider.EmbedAsync("verify onnx publish").GetAwaiter().GetResult();
            if (vector.Length != ExpectedDimensions || !vector.Any(v => v != 0f))
            {
                Console.Error.WriteLine("Embedding produced an empty or wrong-sized vector.");
                return 3;
            }

            Console.WriteLine(
                $"OK: onnx verify passed (dims={provider.Dimensions}, model={modelPath}, vocab={vocabPath})");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ONNX verify failed: {ex}");
            return 10;
        }
    }
}
