using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IsaacAgent.Agent;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using IsaacAgent.App.Views;
using IsaacAgent.LLM;
using IsaacAgent.Rag;
using IsaacAgent.Rag.Embedding;
using IsaacAgent.Rag.Retrieval;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace IsaacAgent.App;

public sealed class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Services = ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        }));

        var config = AppConfiguration.Load();
        services.AddSingleton(config);
        services.AddLlmProvider(new(
            config.ProviderType,
            config.Endpoint,
            config.Model,
            config.ApiKey
        ));

        services.AddRag(config.ToEmbeddingConfig());

        services.AddIsaacAgent();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<ProjectViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<QuickReferenceViewModel>();

        return services.BuildServiceProvider();
    }

    public static void ReloadLlmProvider()
    {
        var config = AppConfiguration.Load();
        var proxy = Services.GetRequiredService<ChatServiceProxy>();
        var newProvider = LlmServiceRegistration.BuildProvider(Services, new ProviderConfig(
            config.ProviderType, config.Endpoint, config.Model, config.ApiKey, 120));
        proxy.Replace(newProvider);
    }

    public static void ReloadEmbeddingProvider()
    {
        var config = AppConfiguration.Load();
        var proxy = Services.GetRequiredService<EmbeddingProviderProxy>();
        var newProvider = RagServiceRegistration.BuildEmbeddingProvider(Services, config.ToEmbeddingConfig());
        proxy.Replace(newProvider);

        if (Services.GetRequiredService<IRetriever>() is Retriever retriever)
        {
            retriever.ResetReady();
            _ = Task.Run(async () =>
            {
                try
                {
                    await retriever.RebuildIndexAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Index rebuild failed: {ex.Message}");
                }
            });
        }
    }
}
