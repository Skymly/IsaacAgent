using IsaacAgent.Rag.Embedding;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IsaacAgent.Tests;

public class DefaultOnnxAssetsTests
{
    [Fact]
    public void ResolveModelPath_Empty_FallsBackToBundled()
    {
        Assert.Equal(DefaultOnnxAssets.BundledModelPath, DefaultOnnxAssets.ResolveModelPath(null));
        Assert.Equal(DefaultOnnxAssets.BundledModelPath, DefaultOnnxAssets.ResolveModelPath(""));
        Assert.Equal(DefaultOnnxAssets.BundledModelPath, DefaultOnnxAssets.ResolveModelPath("   "));
    }

    [Fact]
    public void ResolveModelPath_Configured_UsesOverride()
    {
        var custom = Path.Combine(Path.GetTempPath(), "custom-model.onnx");
        Assert.Equal(custom, DefaultOnnxAssets.ResolveModelPath(custom));
    }

    [Fact]
    public void ResolveVocabPath_Empty_FallsBackToBundled()
    {
        Assert.Equal(DefaultOnnxAssets.BundledVocabPath, DefaultOnnxAssets.ResolveVocabPath(null));
        Assert.Equal(DefaultOnnxAssets.BundledVocabPath, DefaultOnnxAssets.ResolveVocabPath(""));
    }

    [Fact]
    public void EmbeddingConfig_Defaults_ToOnnxWithEmptyPaths()
    {
        var config = new EmbeddingConfig();
        Assert.Equal(EmbeddingSourceType.Onnx, config.Source);
        Assert.Equal("", config.OnnxModelPath);
        Assert.Equal("", config.OnnxTokenizerPath);
    }

    [Fact]
    public void BundledOnnxAssets_ExistNextToTestHost()
    {
        Assert.True(File.Exists(DefaultOnnxAssets.BundledModelPath),
            $"Expected bundled model at {DefaultOnnxAssets.BundledModelPath}");
        Assert.True(File.Exists(DefaultOnnxAssets.BundledVocabPath),
            $"Expected bundled vocab at {DefaultOnnxAssets.BundledVocabPath}");
    }

    [Fact]
    public void EmbeddedOnnxResources_ArePresentInRagAssembly()
    {
        var assembly = typeof(DefaultOnnxAssets).Assembly;
        Assert.NotNull(assembly.GetManifestResourceStream(DefaultOnnxAssets.EmbeddedModelName));
        Assert.NotNull(assembly.GetManifestResourceStream(DefaultOnnxAssets.EmbeddedVocabName));
    }

    [Fact]
    public async Task OnnxEmbeddingProvider_BundledModel_CanEmbed()
    {
        using var provider = new OnnxEmbeddingProvider(
            DefaultOnnxAssets.BundledModelPath,
            DefaultOnnxAssets.BundledVocabPath,
            NullLogger<OnnxEmbeddingProvider>.Instance);

        Assert.Equal(384, provider.Dimensions);
        var vector = await provider.EmbedAsync("create a custom collectible item");
        Assert.Equal(provider.Dimensions, vector.Length);
        Assert.Contains(vector, v => v != 0f);
    }
}
