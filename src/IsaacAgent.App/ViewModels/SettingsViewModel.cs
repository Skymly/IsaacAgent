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

    public SettingsViewModel()
    {
        var config = AppConfiguration.Load();
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
        var config = new AppConfiguration
        {
            ProviderType = SelectedProviderType,
            Endpoint = Endpoint,
            Model = Model,
            ApiKey = ApiKey,
            EmbeddingSource = SelectedEmbeddingSource,
            OllamaEmbeddingEndpoint = OllamaEmbeddingEndpoint,
            OllamaEmbeddingModel = OllamaEmbeddingModel,
            OnnxEmbeddingModelPath = OnnxEmbeddingModelPath,
            OnnxEmbeddingVocabPath = OnnxEmbeddingVocabPath,
        };
        config.Save();
    }
}
