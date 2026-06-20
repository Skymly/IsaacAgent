using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IsaacAgent.App.Services;
using IsaacAgent.LLM;

namespace IsaacAgent.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _endpoint = "";

    [ObservableProperty]
    private string _model = "";

    [ObservableProperty]
    private string? _apiKey;

    [ObservableProperty]
    private ProviderType _selectedProviderType;

    public ObservableCollection<ProviderType> ProviderTypes { get; } = [ProviderType.OpenAICompatible, ProviderType.Ollama];

    public SettingsViewModel()
    {
        var config = AppConfiguration.Load();
        _endpoint = config.Endpoint;
        _model = config.Model;
        _apiKey = config.ApiKey;
        _selectedProviderType = config.ProviderType;
    }

    public void Save()
    {
        var config = new AppConfiguration
        {
            ProviderType = SelectedProviderType,
            Endpoint = Endpoint,
            Model = Model,
            ApiKey = ApiKey
        };
        config.Save();
    }
}
