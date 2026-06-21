namespace IsaacAgent.Rag.Embedding;

public enum EmbeddingSourceType { Ollama, Onnx }

public sealed record EmbeddingConfig
{
    public EmbeddingSourceType Source { get; init; } = EmbeddingSourceType.Ollama;

    public string OllamaEndpoint { get; init; } = "http://localhost:11434";
    public string OllamaModel { get; init; } = "nomic-embed-text";

    public string OnnxModelPath { get; init; } = "";
    public string OnnxTokenizerPath { get; init; } = "";

    public int Dimensions { get; init; } = 0;
}
