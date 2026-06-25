using System.Security.Cryptography;
using System.Text.Json;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;

// AppConfiguration uses DPAPI (ProtectedData) which is Windows-only.
// The App project targets WinExe on Windows with SupportedOSPlatform=windows,
// so CA1416 is expected and suppressed here.
#pragma warning disable CA1416

namespace IsaacAgent.App.Services;

public sealed class AppConfiguration
{
    public ProviderType ProviderType { get; set; } = ProviderType.OpenAICompatible;
    public string Endpoint { get; set; } = "https://api.minimax.chat/v1";
    public string Model { get; set; } = "abab6.5s-chat";

    /// <summary>
    /// The API key. In memory this is plaintext. When saved to disk it is
    /// encrypted with DPAPI (CurrentUser scope) so that only the same Windows
    /// account can decrypt it. The <see cref="EncryptedApiKey"/> field holds
    /// the base64-encoded ciphertext; this property is never serialized directly.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base64-encoded DPAPI-encrypted API key. Used for persistence.
    /// Do not set this directly — use <see cref="ApiKey"/> and <see cref="Save"/>.
    /// </summary>
    public string? EncryptedApiKey { get; set; }

    public EmbeddingSourceType EmbeddingSource { get; set; } = EmbeddingSourceType.Ollama;
    public string OllamaEmbeddingEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaEmbeddingModel { get; set; } = "nomic-embed-text";
    public string? OnnxEmbeddingModelPath { get; set; }
    public string? OnnxEmbeddingVocabPath { get; set; }

    public EmbeddingConfig ToEmbeddingConfig() => new()
    {
        Source = EmbeddingSource,
        OllamaEndpoint = OllamaEmbeddingEndpoint,
        OllamaModel = OllamaEmbeddingModel,
        OnnxModelPath = OnnxEmbeddingModelPath ?? "",
        OnnxTokenizerPath = OnnxEmbeddingVocabPath ?? "",
    };

    private static string GetConfigPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IsaacAgent",
        "config.json");

    public static AppConfiguration Load()
    {
        var configPath = GetConfigPath();

        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json) ?? new();

            if (!string.IsNullOrEmpty(config.EncryptedApiKey))
            {
                config.ApiKey = TryDecryptApiKey(config.EncryptedApiKey);
                if (config.ApiKey is null)
                    config.EncryptedApiKey = null;
            }

            return config;
        }

        var envKey = Environment.GetEnvironmentVariable("ISAAC_AGENT_API_KEY");
        if (!string.IsNullOrEmpty(envKey))
        {
            return new AppConfiguration
            {
                ApiKey = envKey,
                Endpoint = Environment.GetEnvironmentVariable("ISAAC_AGENT_ENDPOINT") ?? "https://api.minimax.chat/v1",
                Model = Environment.GetEnvironmentVariable("ISAAC_AGENT_MODEL") ?? "abab6.5s-chat"
            };
        }

        return new();
    }

    public void Save()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent"
        );
        Directory.CreateDirectory(configDir);

        var toSave = new AppConfiguration
        {
            ProviderType = ProviderType,
            Endpoint = Endpoint,
            Model = Model,
            ApiKey = null,
            EmbeddingSource = EmbeddingSource,
            OllamaEmbeddingEndpoint = OllamaEmbeddingEndpoint,
            OllamaEmbeddingModel = OllamaEmbeddingModel,
            OnnxEmbeddingModelPath = OnnxEmbeddingModelPath,
            OnnxEmbeddingVocabPath = OnnxEmbeddingVocabPath,
        };

        if (!string.IsNullOrEmpty(ApiKey))
        {
            var encrypted = TryEncryptApiKey(ApiKey);
            if (encrypted is not null)
            {
                toSave.EncryptedApiKey = encrypted;
            }
            else
            {
                // DPAPI encryption failed — do NOT fall back to plaintext.
                // Discard the key entirely; the user will need to re-enter it.
                toSave.EncryptedApiKey = null;
                toSave.ApiKey = null;
                System.Diagnostics.Debug.WriteLine(
                    "Warning: DPAPI encryption of the API key failed. "
                    + "The key was not saved. Please re-enter the API key.");
            }
        }

        var configPath = Path.Combine(configDir, "config.json");
        var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
    }

    /// <summary>
    /// Encrypt the API key using DPAPI. Returns base64 ciphertext,
    /// or null if DPAPI is unavailable.
    /// </summary>
    private static string? TryEncryptApiKey(string plaintext)
    {
        try
        {
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            var cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipherBytes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Decrypt the API key using DPAPI. Returns plaintext key,
    /// or null if decryption fails.
    /// </summary>
    private static string? TryDecryptApiKey(string encryptedBase64)
    {
        try
        {
            var cipherBytes = Convert.FromBase64String(encryptedBase64);
            var plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }
}
