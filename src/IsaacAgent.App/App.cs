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
using Serilog;

namespace IsaacAgent.App;

public sealed class App : Application
{
    private static readonly object _reloadLock = new();
    private static readonly CancellationTokenSource _shutdownCts = new();

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
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        // Apply saved language and theme preferences.
        Services.GetRequiredService<LocalizationService>().ApplyInitialLanguage();
        var themeService = Services.GetRequiredService<ThemeService>();
        themeService.ApplyInitialTheme();
        themeService.ApplyInitialAccentColor();

        // Apply saved font size.
        var config = Services.GetRequiredService<AppConfiguration>();
        FontSizeService.ApplyFontSize(string.IsNullOrEmpty(config.FontSize) ? "medium" : config.FontSize);

        // Pre-warm the RAG index in the background so the first search_knowledge
        // call doesn't block the UI for tens of seconds (especially with ONNX).
        _ = Task.Run(() => PrewarmRagIndexAsync(_shutdownCts.Token), _shutdownCts.Token);

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _shutdownCts.Cancel();
        _shutdownCts.Dispose();
        // Flush Serilog buffers on shutdown
        Log.CloseAndFlush();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Load config first to get log level
        var config = AppConfiguration.Load();

        // Configure Serilog: file (JSON, daily rotation) + console
        var loggerFactory = SerilogConfigurator.CreateLoggerFactory(config.LogLevel);
        services.AddSingleton<ILoggerFactory>(loggerFactory);
        services.AddLogging(b => { });

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
        services.AddSingleton<LogMonitorService>();
        services.AddSingleton<DiffService>();
        services.AddSingleton<DiffViewerViewModel>();
        services.AddSingleton<TemplateGalleryViewModel>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<LocalizationService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<ChatHistoryService>();
        services.AddSingleton<LuaSnippetService>();

        return services.BuildServiceProvider();
    }

    public static void ReloadLlmProvider()
    {
        lock (_reloadLock)
        {
            var config = AppConfiguration.Load();
            var proxy = Services.GetRequiredService<ChatServiceProxy>();
            var newProvider = LlmServiceRegistration.BuildProvider(Services, new ProviderConfig(
                config.ProviderType, config.Endpoint, config.Model, config.ApiKey, 120));
            proxy.Replace(newProvider);
        }
    }

    public static void ReloadEmbeddingProvider()
    {
        lock (_reloadLock)
        {
            var config = AppConfiguration.Load();
            var proxy = Services.GetRequiredService<EmbeddingProviderProxy>();
            var newProvider = RagServiceRegistration.BuildEmbeddingProvider(Services, config.ToEmbeddingConfig());
            proxy.Replace(newProvider);

            if (Services.GetRequiredService<IRetriever>() is Retriever retriever)
            {
                retriever.ResetReady();
                var settings = Services.GetService<SettingsViewModel>();
                settings?.SetIndexRebuilding(true);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await retriever.RebuildIndexAsync(_shutdownCts.Token);
                        settings?.SetIndexStatus("Index rebuilt successfully.");
                        Services.GetService<ToastService>()?.ShowSuccess("Knowledge index rebuilt successfully.");
                    }
                    catch (Exception ex)
                    {
                        var logger = Services.GetRequiredService<ILogger<App>>();
                        logger.LogError(ex, "Index rebuild failed");
                        settings?.SetIndexStatus($"Index rebuild failed: {ex.Message}");
                        Services.GetService<ToastService>()?.ShowError($"Index rebuild failed: {ex.Message}");
                    }
                    finally
                    {
                        settings?.SetIndexRebuilding(false);
                    }
                }, _shutdownCts.Token);
            }
        }
    }

    private static async Task PrewarmRagIndexAsync(CancellationToken ct)
    {
        try
        {
            var retriever = Services.GetService<IRetriever>();
            if (retriever is null) return;
            await retriever.EnsureIndexAsync(ct);
        }
        catch (Exception ex)
        {
            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogWarning(ex, "RAG index prewarm failed (will retry on first search)");

            // Surface the failure in the Settings UI so the user understands
            // why search_knowledge may be slow or unavailable on first use.
            var settings = Services.GetService<SettingsViewModel>();
            settings?.SetIndexStatus($"Knowledge index build failed: {ex.Message}. Will retry on first search.");
            Services.GetService<ToastService>()?.ShowWarning("Knowledge index build failed — will retry on first search.");
        }
    }
}
