using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.LLM.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.LLM;

public enum ProviderType { OpenAICompatible, Ollama }

public sealed record ProviderConfig(
    ProviderType Type,
    string Endpoint,
    string Model,
    string? ApiKey = null,
    int TimeoutSeconds = 120
);

public static class LlmServiceRegistration
{
    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10)
    ];

    public static IServiceCollection AddLlmProvider(this IServiceCollection services, ProviderConfig config)
    {
        services.AddSingleton(config);
        services.AddSingleton<ChatServiceProxy>(sp => new ChatServiceProxy(BuildProvider(sp, config)));
        services.AddSingleton<IChatService>(sp => sp.GetRequiredService<ChatServiceProxy>());
        return services;
    }

    public static IChatService BuildProvider(IServiceProvider sp, ProviderConfig config)
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(config.Endpoint),
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        if (!string.IsNullOrEmpty(config.ApiKey))
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

        IChatService inner = config.Type switch
        {
            ProviderType.OpenAICompatible => new OpenAICompatibleProvider(
                http, config.Model, sp.GetRequiredService<ILogger<OpenAICompatibleProvider>>()),
            ProviderType.Ollama => new OllamaProvider(
                http, config.Model, sp.GetRequiredService<ILogger<OllamaProvider>>()),
            _ => throw new ArgumentException($"Unknown provider type: {config.Type}")
        };

        var retryLogger = sp.GetRequiredService<ILogger<RetryChatService>>();
        return new RetryChatService(inner, MaxRetries, RetryDelays, retryLogger);
    }
}
