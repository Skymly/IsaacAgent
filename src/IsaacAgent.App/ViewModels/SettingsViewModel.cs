using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IsaacAgent.App.Services;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;
using Microsoft.Extensions.DependencyInjection;

namespace IsaacAgent.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    // LLM settings
    [ObservableProperty]
    private string _endpoint = "";

    [ObservableProperty]
    private string _model = "";

    [ObservableProperty]
    private string? _apiKey;

    [ObservableProperty]
    private ProviderType _selectedProviderType;

    public ObservableCollection<ProviderType> ProviderTypes { get; } = [ProviderType.OpenAICompatible, ProviderType.Ollama];

    // RAG / Embedding settings
    [ObservableProperty]
    private EmbeddingSourceType _selectedEmbeddingSource;

    public ObservableCollection<EmbeddingSourceType> EmbeddingSources { get; } = [EmbeddingSourceType.Ollama, EmbeddingSourceType.Onnx];

    [ObservableProperty]
    private string _ollamaEmbeddingEndpoint = "";

    [ObservableProperty]
    private string _ollamaEmbeddingModel = "";

    [ObservableProperty]
    private string? _onnxEmbeddingModelPath;

    [ObservableProperty]
    private string? _onnxEmbeddingVocabPath;

    [ObservableProperty]
    private bool _isRebuildingIndex;

    [ObservableProperty]
    private string _indexStatus = "";

    // Appearance settings
    [ObservableProperty]
    private string _selectedLanguage = "en";

    public ObservableCollection<string> AvailableLanguages { get; } = ["en", "zh", "ja", "ko"];

    [ObservableProperty]
    private string _selectedTheme = "dark";

    public ObservableCollection<string> AvailableThemes { get; } = ["dark", "light"];

    [ObservableProperty]
    private string? _accentColor;

    [ObservableProperty]
    private string _selectedFontSize = "medium";

    public ObservableCollection<string> AvailableFontSizes { get; } = ["small", "medium", "large"];

    [ObservableProperty]
    private string _selectedLogLevel = "Information";

    public ObservableCollection<string> AvailableLogLevels { get; } =
        ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    private readonly AppConfiguration _config;

    public SettingsViewModel(AppConfiguration config)
    {
        _config = config;
        _endpoint = config.Endpoint;
        _model = config.Model;
        _apiKey = config.ApiKey;
        _selectedProviderType = config.ProviderType;
        _selectedEmbeddingSource = config.EmbeddingSource;
        _ollamaEmbeddingEndpoint = config.OllamaEmbeddingEndpoint;
        _ollamaEmbeddingModel = config.OllamaEmbeddingModel;
        _onnxEmbeddingModelPath = config.OnnxEmbeddingModelPath;
        _onnxEmbeddingVocabPath = config.OnnxEmbeddingVocabPath;
        _selectedLanguage = string.IsNullOrEmpty(config.Language) ? "en" : config.Language;
        _selectedTheme = string.IsNullOrEmpty(config.Theme) ? "dark" : config.Theme;
        _accentColor = config.AccentColor;
        _selectedFontSize = string.IsNullOrEmpty(config.FontSize) ? "medium" : config.FontSize;
        _selectedLogLevel = string.IsNullOrEmpty(config.LogLevel) ? "Information" : config.LogLevel;
    }

    public void Save()
    {
        // Keep the DI-managed singleton in sync so other consumers see the
        // updated values without re-reading from disk.
        _config.ProviderType = SelectedProviderType;
        _config.Endpoint = Endpoint;
        _config.Model = Model;
        _config.ApiKey = ApiKey;
        _config.EmbeddingSource = SelectedEmbeddingSource;
        _config.OllamaEmbeddingEndpoint = OllamaEmbeddingEndpoint;
        _config.OllamaEmbeddingModel = OllamaEmbeddingModel;
        _config.OnnxEmbeddingModelPath = OnnxEmbeddingModelPath;
        _config.OnnxEmbeddingVocabPath = OnnxEmbeddingVocabPath;

        // Apply language and theme changes at runtime.
        var languageChanged = _config.Language != SelectedLanguage;
        var themeChanged = _config.Theme != SelectedTheme;
        var accentChanged = _config.AccentColor != AccentColor;
        _config.Language = SelectedLanguage;
        _config.Theme = SelectedTheme;
        _config.AccentColor = AccentColor;
        _config.FontSize = SelectedFontSize;
        _config.LogLevel = SelectedLogLevel;

        _config.Save();
        App.ReloadLlmProvider();
        App.ReloadEmbeddingProvider();

        if (languageChanged)
            App.Services.GetRequiredService<LocalizationService>().SetLanguage(SelectedLanguage);
        if (themeChanged)
            App.Services.GetRequiredService<ThemeService>().SetTheme(SelectedTheme);
        if (accentChanged)
            App.Services.GetRequiredService<ThemeService>().ApplyAccentColor(AccentColor);

        FontSizeService.ApplyFontSize(SelectedFontSize);
    }

    /// <summary>
    /// Called from <see cref="App.ReloadEmbeddingProvider"/> to reflect index
    /// rebuild progress in the UI. Safe to call from any thread.
    /// </summary>
    public void SetIndexRebuilding(bool value)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => IsRebuildingIndex = value);
    }

    /// <summary>
    /// Called from <see cref="App.ReloadEmbeddingProvider"/> to report index
    /// rebuild success/failure. Safe to call from any thread.
    /// </summary>
    public void SetIndexStatus(string status)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => IndexStatus = status);
    }
}
