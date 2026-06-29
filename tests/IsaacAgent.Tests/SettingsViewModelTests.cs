using Avalonia.Threading;
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
///   SetIndexStatus/SetIndexRebuilding use Dispatcher.UIThread.Post.
/// </remarks>
[Collection("Avalonia")]
public class SettingsViewModelTests
{
    private static SettingsViewModel CreateViewModel(AppConfiguration? config = null)
    {
        config ??= new AppConfiguration();
        return new SettingsViewModel(config);
    }

    private static void FlushDispatcher()
    {
        if (Dispatcher.UIThread.CheckAccess())
            Dispatcher.UIThread.RunJobs();
    }

    [Fact]
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

    [Fact]
    public void Constructor_Defaults_IndexStatusIsEmpty()
    {
        var vm = CreateViewModel();
        Assert.Equal("", vm.IndexStatus);
        Assert.False(vm.IsRebuildingIndex);
    }

    [Fact]
    public void ProviderTypes_ContainsBothOptions()
    {
        var vm = CreateViewModel();
        Assert.Equal(2, vm.ProviderTypes.Count);
        Assert.Contains(ProviderType.OpenAICompatible, vm.ProviderTypes);
        Assert.Contains(ProviderType.Ollama, vm.ProviderTypes);
    }

    [Fact]
    public void EmbeddingSources_ContainsBothOptions()
    {
        var vm = CreateViewModel();
        Assert.Equal(2, vm.EmbeddingSources.Count);
        Assert.Contains(EmbeddingSourceType.Ollama, vm.EmbeddingSources);
        Assert.Contains(EmbeddingSourceType.Onnx, vm.EmbeddingSources);
    }

    [Fact(Skip = "SetIndexStatus uses Dispatcher.UIThread.Post which can't be reliably flushed in headless test runner when run as part of full suite")]
    public void SetIndexStatus_UpdatesProperty()
    {
        var vm = CreateViewModel();
        vm.SetIndexStatus("Building index...");
        FlushDispatcher();
        Assert.Equal("Building index...", vm.IndexStatus);
    }

    [Fact(Skip = "SetIndexRebuilding uses Dispatcher.UIThread.Post which can't be reliably flushed in headless test runner when run as part of full suite")]
    public void SetIndexRebuilding_True_SetsProperty()
    {
        var vm = CreateViewModel();
        vm.SetIndexRebuilding(true);
        FlushDispatcher();
        Assert.True(vm.IsRebuildingIndex);
    }

    [Fact(Skip = "SetIndexRebuilding uses Dispatcher.UIThread.Post which can't be reliably flushed in headless test runner when run as part of full suite")]
    public void SetIndexRebuilding_False_SetsProperty()
    {
        var vm = CreateViewModel();
        vm.SetIndexRebuilding(true);
        FlushDispatcher();
        vm.SetIndexRebuilding(false);
        FlushDispatcher();
        Assert.False(vm.IsRebuildingIndex);
    }

    [Fact]
    public void Endpoint_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.Endpoint = "https://new.endpoint/v1";
        Assert.Equal("https://new.endpoint/v1", vm.Endpoint);
    }

    [Fact]
    public void Model_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.Model = "claude-3";
        Assert.Equal("claude-3", vm.Model);
    }

    [Fact]
    public void ApiKey_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.ApiKey = "new-key";
        Assert.Equal("new-key", vm.ApiKey);
    }

    [Fact]
    public void SelectedProviderType_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.SelectedProviderType = ProviderType.Ollama;
        Assert.Equal(ProviderType.Ollama, vm.SelectedProviderType);
    }

    [Fact]
    public void SelectedEmbeddingSource_SetAndGet_WorksCorrectly()
    {
        var vm = CreateViewModel();
        vm.SelectedEmbeddingSource = EmbeddingSourceType.Onnx;
        Assert.Equal(EmbeddingSourceType.Onnx, vm.SelectedEmbeddingSource);
    }

    [Fact]
    public void Constructor_WithNullApiKey_DoesNotThrow()
    {
        var config = new AppConfiguration { ApiKey = null };
        var vm = new SettingsViewModel(config);
        Assert.Null(vm.ApiKey);
    }

    [Fact]
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
}
