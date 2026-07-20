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

        services.AddSingleton<EmbeddingProviderProxy>(sp => new EmbeddingProviderProxy(BuildEmbeddingProvider(sp, embeddingConfig)));
        services.AddSingleton<IEmbeddingProvider>(sp => sp.GetRequiredService<EmbeddingProviderProxy>());

        services.AddSingleton<IndexBuilder>(sp =>
        {
            var embedding = sp.GetRequiredService<IEmbeddingProvider>();
            var store = sp.GetRequiredService<InMemoryVectorStore>();
            var logger = sp.GetRequiredService<ILogger<IndexBuilder>>();
            return new IndexBuilder(embedding, store, examplesDir, logger);
        });

        services.AddSingleton<Retriever>(sp =>
        {
            var embedding = sp.GetRequiredService<IEmbeddingProvider>();
            var store = sp.GetRequiredService<InMemoryVectorStore>();
            var builder = sp.GetRequiredService<IndexBuilder>();
            var logger = sp.GetRequiredService<ILogger<Retriever>>();
            return new Retriever(embedding, store, builder, indexPath, logger);
        });
        services.AddSingleton<IRetriever>(sp => sp.GetRequiredService<Retriever>());

        services.AddSingleton<EmbeddingApply>(sp => new EmbeddingApply(
            sp.GetRequiredService<EmbeddingProviderProxy>(),
            sp.GetRequiredService<Retriever>(),
            sp.GetRequiredService<InMemoryVectorStore>(),
            indexPath));

        return services;
    }

    public static IEmbeddingProvider BuildEmbeddingProvider(IServiceProvider sp, EmbeddingConfig config)
    {
        return config.Source switch
        {
            EmbeddingSourceType.Ollama => new OllamaEmbeddingProvider(
                new HttpClient { BaseAddress = new Uri(config.OllamaEndpoint) },
                config.OllamaModel,
                sp.GetRequiredService<ILogger<OllamaEmbeddingProvider>>()),
            EmbeddingSourceType.Onnx => new OnnxEmbeddingProvider(
                DefaultOnnxAssets.ResolveModelPath(config.OnnxModelPath),
                DefaultOnnxAssets.ResolveVocabPath(config.OnnxTokenizerPath),
                sp.GetRequiredService<ILogger<OnnxEmbeddingProvider>>()),
            _ => throw new ArgumentException($"Unknown embedding source: {config.Source}")
        };
    }
}
