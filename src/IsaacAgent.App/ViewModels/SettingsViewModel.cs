using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using IsaacAgent.Agent.Engine;
using IsaacAgent.App.Services;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;

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

    public ObservableCollection<EmbeddingSourceType> EmbeddingSources { get; } = [EmbeddingSourceType.Onnx, EmbeddingSourceType.Ollama];

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

    // Agent settings
    [ObservableProperty]
    private HandEditConflictMode _selectedHandEditConflictMode = HandEditConflictMode.Force;

    public ObservableCollection<HandEditConflictMode> HandEditConflictModes { get; } =
        [HandEditConflictMode.Force, HandEditConflictMode.Skip];

    private readonly AppConfiguration _config;
    private readonly ISettingsApply _settingsApply;
    private readonly ToastService? _toasts;
    private readonly LocalizationService? _localization;
    private readonly ThemeService? _theme;

    public SettingsViewModel(
        AppConfiguration config,
        ISettingsApply? settingsApply = null,
        ToastService? toasts = null,
        LocalizationService? localization = null,
        ThemeService? theme = null)
    {
        _config = config;
        _settingsApply = settingsApply ?? NoOpSettingsApply.Instance;
        _toasts = toasts;
        _localization = localization;
        _theme = theme;
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
        _selectedHandEditConflictMode = config.HandEditConflictMode;
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

        // Apply language and theme changes at runtime (chrome — not Settings apply).
        var languageChanged = _config.Language != SelectedLanguage;
        var themeChanged = _config.Theme != SelectedTheme;
        var accentChanged = _config.AccentColor != AccentColor;
        _config.Language = SelectedLanguage;
        _config.Theme = SelectedTheme;
        _config.AccentColor = AccentColor;
        _config.FontSize = SelectedFontSize;
        _config.LogLevel = SelectedLogLevel;
        _config.HandEditConflictMode = SelectedHandEditConflictMode;

        _config.Save();

        var intent = new ProviderIntent(
            new ProviderConfig(
                SelectedProviderType,
                Endpoint,
                Model,
                ApiKey),
            _config.ToEmbeddingConfig());
        _settingsApply.Apply(intent, new SettingsApplyProgress(this, _toasts));

        if (languageChanged)
            _localization?.SetLanguage(SelectedLanguage);
        if (themeChanged)
            _theme?.SetTheme(SelectedTheme);
        if (accentChanged)
            _theme?.ApplyAccentColor(AccentColor);

        FontSizeService.ApplyFontSize(SelectedFontSize);
    }

    /// <summary>
    /// Reflects knowledge-index rebuild progress in the UI. Safe to call from any thread.
    /// </summary>
    public void SetIndexRebuilding(bool value)
    {
        Dispatcher.UIThread.Post(() => IsRebuildingIndex = value);
    }

    /// <summary>
    /// Reports knowledge-index rebuild success/failure. Safe to call from any thread.
    /// </summary>
    public void SetIndexStatus(string status)
    {
        Dispatcher.UIThread.Post(() => IndexStatus = status);
    }
}
