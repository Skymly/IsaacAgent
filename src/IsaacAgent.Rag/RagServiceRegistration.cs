using IsaacAgent.Core.Services;
using IsaacAgent.Rag.Embedding;
using IsaacAgent.Rag.Indexing;
using IsaacAgent.Rag.Retrieval;
using IsaacAgent.Rag.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Rag;

public static class RagServiceRegistration
{
    public static IServiceCollection AddRag(this IServiceCollection services, EmbeddingConfig embeddingConfig, string? dataDir = null)
    {
        dataDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent",
            "rag");

        Directory.CreateDirectory(dataDir);

        var indexPath = Path.Combine(dataDir, "index.bin");
        var examplesDir = Path.Combine(dataDir, "examples");

        services.AddSingleton(embeddingConfig);
        services.AddSingleton<InMemoryVectorStore>();

        services.AddSingleton<IEmbeddingProvider>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<InMemoryVectorStore>>();
            return embeddingConfig.Source switch
            {
                EmbeddingSourceType.Ollama => new OllamaEmbeddingProvider(
                    new HttpClient { BaseAddress = new Uri(embeddingConfig.OllamaEndpoint) },
                    embeddingConfig.OllamaModel,
                    sp.GetRequiredService<ILogger<OllamaEmbeddingProvider>>()),
                EmbeddingSourceType.Onnx => new OnnxEmbeddingProvider(
                    embeddingConfig.OnnxModelPath,
                    embeddingConfig.OnnxTokenizerPath,
                    sp.GetRequiredService<ILogger<OnnxEmbeddingProvider>>()),
                _ => throw new ArgumentException($"Unknown embedding source: {embeddingConfig.Source}")
            };
        });

        services.AddSingleton<IndexBuilder>(sp =>
        {
            var embedding = sp.GetRequiredService<IEmbeddingProvider>();
            var store = sp.GetRequiredService<InMemoryVectorStore>();
            var logger = sp.GetRequiredService<ILogger<IndexBuilder>>();
            return new IndexBuilder(embedding, store, examplesDir, logger);
        });

        services.AddSingleton<IRetriever>(sp =>
        {
            var embedding = sp.GetRequiredService<IEmbeddingProvider>();
            var store = sp.GetRequiredService<InMemoryVectorStore>();
            var builder = sp.GetRequiredService<IndexBuilder>();
            var logger = sp.GetRequiredService<ILogger<Retriever>>();
            return new Retriever(embedding, store, builder, indexPath, logger);
        });

        return services;
    }
}
