namespace IsaacAgent.Rag.Embedding;

public enum EmbeddingSourceType { Ollama, Onnx }

public sealed record EmbeddingConfig
{
    /// <summary>Default is ONNX with bundled all-MiniLM-L6-v2 (ADR-002).</summary>
    public EmbeddingSourceType Source { get; init; } = EmbeddingSourceType.Onnx;

    public string OllamaEndpoint { get; init; } = "http://localhost:11434";
    public string OllamaModel { get; init; } = "nomic-embed-text";

    /// <summary>Empty uses <see cref="DefaultOnnxAssets.BundledModelPath"/>.</summary>
    public string OnnxModelPath { get; init; } = "";

    /// <summary>Empty uses <see cref="DefaultOnnxAssets.BundledVocabPath"/>.</summary>
    public string OnnxTokenizerPath { get; init; } = "";

    public int Dimensions { get; init; } = 0;
}
