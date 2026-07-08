using Avalonia.Headless.XUnit;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for SettingsViewModel — config loading, property sync,
///   and index status reporting.
/// </summary>
/// <remarks>
///   Save() calls App.ReloadLlmProvider/ReloadEmbeddingProvider which
///   require a fully initialized DI container, so it is not tested here.
///   SetIndexStatus/SetIndexRebuilding marshal to the UI thread when needed.
/// </remarks>
[Collection("Avalonia")]
public class SettingsViewModelTests
{
    private static SettingsViewModel CreateViewModel(AppConfiguration? config = null)
    {
        config ??= new AppConfiguration();
        return new SettingsViewModel(config);
    }

    [AvaloniaFact]
    public void Constructor_LoadsConfigValues()
    {
        var config = new AppConfiguration
        {
            Endpoint = "https://custom.api/v1",
            Model = "gpt-4",
            ApiKey = "secret-key",
            ProviderType = ProviderType.Ollama,
            EmbeddingSource = EmbeddingSourceType.Onnx,
            OllamaEmbeddingEndpoint = "http://localhost:11434",
            OllamaEmbeddingModel = "nomic-embed-text",
            OnnxEmbeddingModelPath = "/path/to/model.onnx",
            OnnxEmbeddingVocabPath = "/path/to/vocab.txt"
        };
        var vm = new SettingsViewModel(config);

        Assert.Equal("https://custom.api/v1", vm.Endpoint);
        Assert.Equal("gpt-4", vm.Model);
        Assert.Equal("secret-key", vm.ApiKey);
        Assert.Equal(ProviderType.Ollama, vm.SelectedProviderType);
        Assert.Equal(EmbeddingSourceType.Onnx, vm.SelectedEmbeddingSource);
        Assert.Equal("http://localhost:11434", vm.OllamaEmbeddingEndpoint);
        Assert.Equal("nomic-embed-text", vm.OllamaEmbeddingModel);
        Assert.Equal("/path/to/model.onnx", vm.OnnxEmbeddingModelPath);
        Assert.Equal("/path/to/vocab.txt", vm.OnnxEmbeddingVocabPath);
    }

    [AvaloniaFact]
    public void Constructor_Defaults_IndexStatusIsEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal("", vm.IndexStatus);
        Assert.False(vm.IsRebuildingIndex);
    }

    [AvaloniaFact]
    public void ProviderTypes_ContainsBothOptions()
    {
        var vm = CreateViewModel();
        Assert.Equal(2, vm.ProviderTypes.Count);
        Assert.Contains(ProviderType.OpenAICompatible, vm.ProviderTypes);
        Assert.Contains(ProviderType.Ollama, vm.ProviderTypes);
    }

    [AvaloniaFact]
    public void EmbeddingSources_ContainsBothOptions()
    {
        var vm = CreateViewModel();
        Assert.Equal(2, vm.EmbeddingSources.Count);
        Assert.Contains(EmbeddingSourceType.Ollama, vm.EmbeddingSources);
        Assert.Contains(EmbeddingSourceType.Onnx, vm.EmbeddingSources);
    }

    [AvaloniaFact]
    public void SetIndexStatus_UpdatesProperty()
    {
        var vm = CreateViewModel();
        vm.SetIndexStatus("Building index...");
        AvaloniaTestHelper.FlushDispatcher();
        Assert.Equal("Building index...", vm.IndexStatus);
    }

    [AvaloniaFact]
    public void SetIndexRebuilding_True_SetsProperty()
    {
        var vm = CreateViewModel();
        vm.SetIndexRebuilding(true);
        AvaloniaTestHelper.FlushDispatcher();
        Assert.True(vm.IsRebuildingIndex);
    }

    [AvaloniaFact]
    public void SetIndexRebuilding_False_SetsProperty()
    {
        var vm = CreateViewModel();
        vm.SetIndexRebuilding(true);
        AvaloniaTestHelper.FlushDispatcher();
        vm.SetIndexRebuilding(false);
        AvaloniaTestHelper.FlushDispatcher();
        Assert.False(vm.IsRebuildingIndex);
    }

    [AvaloniaFact]
    public void Endpoint_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.Endpoint = "https://new.endpoint/v1";
        Assert.Equal("https://new.endpoint/v1", vm.Endpoint);
    }

    [AvaloniaFact]
    public void Model_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.Model = "claude-3";
        Assert.Equal("claude-3", vm.Model);
    }

    [AvaloniaFact]
    public void ApiKey_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.ApiKey = "new-key";
        Assert.Equal("new-key", vm.ApiKey);
    }

    [AvaloniaFact]
    public void SelectedProviderType_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.SelectedProviderType = ProviderType.Ollama;
        Assert.Equal(ProviderType.Ollama, vm.SelectedProviderType);
    }

    [AvaloniaFact]
    public void SelectedEmbeddingSource_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.SelectedEmbeddingSource = EmbeddingSourceType.Onnx;
        Assert.Equal(EmbeddingSourceType.Onnx, vm.SelectedEmbeddingSource);
    }

    [AvaloniaFact]
    public void Constructor_WithNullApiKey_DoesNotThrow()
    {
        var config = new AppConfiguration { ApiKey = null };
        var vm = new SettingsViewModel(config);
        Assert.Null(vm.ApiKey);
    }

    [AvaloniaFact]
    public void Constructor_WithEmptyStrings_LoadsCorrectly()
    {
        var config = new AppConfiguration
        {
            Endpoint = "",
            Model = "",
            OllamaEmbeddingEndpoint = "",
            OllamaEmbeddingModel = ""
        };
        var vm = new SettingsViewModel(config);
        Assert.Equal("", vm.Endpoint);
        Assert.Equal("", vm.Model);
    }

    [AvaloniaFact]
    public void Constructor_LoadsLanguageFromConfig()
    {
        var config = new AppConfiguration { Language = "zh" };
        var vm = new SettingsViewModel(config);
        Assert.Equal("zh", vm.SelectedLanguage);
    }

    [AvaloniaFact]
    public void Constructor_LoadsThemeFromConfig()
    {
        var config = new AppConfiguration { Theme = "light" };
        var vm = new SettingsViewModel(config);
        Assert.Equal("light", vm.SelectedTheme);
    }

    [AvaloniaFact]
    public void Constructor_DefaultLanguage_IsEnglish()
    {
        var vm = CreateViewModel();
        Assert.Equal("en", vm.SelectedLanguage);
    }

    [AvaloniaFact]
    public void Constructor_DefaultTheme_IsDark()
    {
        var vm = CreateViewModel();
        Assert.Equal("dark", vm.SelectedTheme);
    }

    [AvaloniaFact]
    public void AvailableLanguages_ContainsAllFour()
    {
        var vm = CreateViewModel();
        Assert.Equal(4, vm.AvailableLanguages.Count);
        Assert.Contains("en", vm.AvailableLanguages);
        Assert.Contains("zh", vm.AvailableLanguages);
        Assert.Contains("ja", vm.AvailableLanguages);
        Assert.Contains("ko", vm.AvailableLanguages);
    }

    [AvaloniaFact]
    public void AvailableThemes_ContainsDarkAndLight()
    {
        var vm = CreateViewModel();
        Assert.Equal(2, vm.AvailableThemes.Count);
        Assert.Contains("dark", vm.AvailableThemes);
        Assert.Contains("light", vm.AvailableThemes);
    }

    [AvaloniaFact]
    public void SelectedLanguage_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.SelectedLanguage = "zh";
        Assert.Equal("zh", vm.SelectedLanguage);
    }

    [AvaloniaFact]
    public void SelectedTheme_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.SelectedTheme = "light";
        Assert.Equal("light", vm.SelectedTheme);
    }
}
