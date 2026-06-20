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
    string? ApiKey = null
);

public static class LlmServiceRegistration
{
    public static IServiceCollection AddLlmProvider(this IServiceCollection services, ProviderConfig config)
    {
        services.AddSingleton(config);
        services.AddSingleton<IChatService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OpenAICompatibleProvider>>();
            var http = new HttpClient { BaseAddress = new Uri(config.Endpoint) };

            if (!string.IsNullOrEmpty(config.ApiKey))
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

            return config.Type switch
            {
                ProviderType.OpenAICompatible => new OpenAICompatibleProvider(http, config.Model, logger),
                ProviderType.Ollama => new OllamaProvider(http, config.Model, sp.GetRequiredService<ILogger<OllamaProvider>>()),
                _ => throw new ArgumentException($"Unknown provider type: {config.Type}")
            };
        });
        return services;
    }
}
