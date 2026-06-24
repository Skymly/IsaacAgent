using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
        _config.Save();
        App.ReloadLlmProvider();
        App.ReloadEmbeddingProvider();
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
