using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IsaacAgent.Agent;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using IsaacAgent.App.Views;
using IsaacAgent.Core.Services;
using IsaacAgent.LLM;
using IsaacAgent.Rag;
using IsaacAgent.Rag.Embedding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace IsaacAgent.App;

public sealed class App : Application
{
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

        services.AddSingleton<IEmbeddingApply>(sp =>
            new EmbeddingApplyAdapter(sp.GetRequiredService<EmbeddingApply>()));
        services.AddSingleton<ISettingsApply>(sp => new SettingsApply(
            sp.GetRequiredService<ChatServiceProxy>(),
            cfg => LlmServiceRegistration.BuildProvider(sp, cfg),
            sp.GetRequiredService<IEmbeddingApply>(),
            emb => RagServiceRegistration.BuildEmbeddingProvider(sp, emb),
            config.ToEmbeddingConfig(),
            _shutdownCts.Token,
            sp.GetRequiredService<ILogger<SettingsApply>>()));

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
