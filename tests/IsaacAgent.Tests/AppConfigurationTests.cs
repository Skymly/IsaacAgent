using System.Text.Json;
using IsaacAgent.Agent.Engine;
using IsaacAgent.App.Services;
using IsaacAgent.Rag.Embedding;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for AppConfiguration — defaults, JSON serialization
///   round-trip, and Save/Load persistence (the latter touches the real
///   AppData config path with backup/restore).
/// </summary>
public class AppConfigurationTests
{
    [Fact]
    public void EmbeddingSource_Defaults_ToOnnx()
    {
        Assert.Equal(EmbeddingSourceType.Onnx, new AppConfiguration().EmbeddingSource);
    }

    [Fact]
    public void WindowState_Defaults_AreZero()
    {
        var config = new AppConfiguration();
        Assert.Equal(0, config.WindowWidth);
        Assert.Equal(0, config.WindowHeight);
        Assert.Null(config.WindowX);
        Assert.Null(config.WindowY);
        Assert.False(config.WindowMaximized);
    }

    [Fact]
    public void WindowState_RoundTrips_ThroughJsonSerialization()
    {
        var original = new AppConfiguration
        {
            WindowWidth = 1400,
            WindowHeight = 900,
            WindowX = 100,
            WindowY = 50,
            WindowMaximized = false
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppConfiguration>(json)!;

        Assert.Equal(1400, restored.WindowWidth);
        Assert.Equal(900, restored.WindowHeight);
        Assert.Equal(100, restored.WindowX);
        Assert.Equal(50, restored.WindowY);
        Assert.False(restored.WindowMaximized);
    }

    [Fact]
    public void WindowState_Maximized_RoundTrips()
    {
        var original = new AppConfiguration
        {
            WindowWidth = 1920,
            WindowHeight = 1080,
            WindowMaximized = true
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppConfiguration>(json)!;

        Assert.True(restored.WindowMaximized);
        Assert.Equal(1920, restored.WindowWidth);
    }

    [Fact]
    public void WindowState_NullPosition_RoundTrips()
    {
        var original = new AppConfiguration
        {
            WindowWidth = 800,
            WindowHeight = 600,
            WindowX = null,
            WindowY = null
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppConfiguration>(json)!;

        Assert.Null(restored.WindowX);
        Assert.Null(restored.WindowY);
    }

    [Fact]
    public void WindowState_NegativePosition_RoundTrips()
    {
        // Multi-monitor setups can have negative coordinates
        var original = new AppConfiguration
        {
            WindowWidth = 1200,
            WindowHeight = 800,
            WindowX = -1920,
            WindowY = -200
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppConfiguration>(json)!;

        Assert.Equal(-1920, restored.WindowX);
        Assert.Equal(-200, restored.WindowY);
    }

    [Fact]
    public void WindowState_PreservesOtherConfigFields()
    {
        var original = new AppConfiguration
        {
            Endpoint = "https://custom.api/v1",
            Model = "gpt-4",
            WindowWidth = 1600,
            WindowHeight = 1000,
            WindowMaximized = true
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppConfiguration>(json)!;

        Assert.Equal("https://custom.api/v1", restored.Endpoint);
        Assert.Equal("gpt-4", restored.Model);
        Assert.Equal(1600, restored.WindowWidth);
        Assert.True(restored.WindowMaximized);
    }

    [Fact]
    public void ApiKey_IsNotSerializedToJson()
    {
        var original = new AppConfiguration
        {
            ApiKey = "sk-super-secret",
            EncryptedApiKey = "ciphertext-base64",
            Endpoint = "https://custom.api/v1"
        };

        var json = JsonSerializer.Serialize(original);

        Assert.DoesNotContain("sk-super-secret", json);
        Assert.DoesNotContain("\"ApiKey\"", json);
        Assert.Contains("EncryptedApiKey", json);
        Assert.Contains("ciphertext-base64", json);

        var restored = JsonSerializer.Deserialize<AppConfiguration>(json)!;
        Assert.Null(restored.ApiKey);
        Assert.Equal("ciphertext-base64", restored.EncryptedApiKey);
    }

    [Fact]
    public void HandEditConflictMode_Defaults_ToForce()
    {
        Assert.Equal(HandEditConflictMode.Force, new AppConfiguration().HandEditConflictMode);
    }

    [Theory]
    [InlineData(HandEditConflictMode.Force)]
    [InlineData(HandEditConflictMode.Skip)]
    public void HandEditConflictMode_RoundTrips_ThroughJsonSerialization(HandEditConflictMode mode)
    {
        var original = new AppConfiguration { HandEditConflictMode = mode };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppConfiguration>(json)!;

        Assert.Equal(mode, restored.HandEditConflictMode);
    }

    [Fact]
    public void HandEditConflictMode_Persists_ThroughSaveAndLoad()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent",
            "config.json");
        string? backup = File.Exists(configPath) ? File.ReadAllText(configPath) : null;

        try
        {
            var config = new AppConfiguration
            {
                HandEditConflictMode = HandEditConflictMode.Skip,
                Endpoint = "https://test.example/v1",
                Model = "test-model",
            };
            config.Save();

            var loaded = AppConfiguration.Load();
            Assert.Equal(HandEditConflictMode.Skip, loaded.HandEditConflictMode);
        }
        finally
        {
            if (backup is not null)
                File.WriteAllText(configPath, backup);
            else if (File.Exists(configPath))
                File.Delete(configPath);
        }
    }
}
