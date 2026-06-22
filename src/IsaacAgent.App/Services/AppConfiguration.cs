using System.Security.Cryptography;
using System.Text.Json;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;

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

            // Decrypt the API key from DPAPI-protected storage
            if (!string.IsNullOrEmpty(config.EncryptedApiKey))
            {
                try
                {
                    var cipherBytes = Convert.FromBase64String(config.EncryptedApiKey);
                    var plainBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
                    config.ApiKey = System.Text.Encoding.UTF8.GetString(plainBytes);
                }
                catch
                {
                    // Decryption failed (e.g. different user account, corrupted data)
                    // — fall back to empty key, user will need to re-enter it.
                    config.ApiKey = null;
                    config.EncryptedApiKey = null;
                }
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

        // Encrypt the API key with DPAPI before writing to disk.
        // We create a temporary copy so the in-memory ApiKey stays plaintext
        // for the current session, but only the encrypted form is persisted.
        var toSave = new AppConfiguration
        {
            ProviderType = ProviderType,
            Endpoint = Endpoint,
            Model = Model,
            ApiKey = null, // Never serialize plaintext
            EmbeddingSource = EmbeddingSource,
            OllamaEmbeddingEndpoint = OllamaEmbeddingEndpoint,
            OllamaEmbeddingModel = OllamaEmbeddingModel,
            OnnxEmbeddingModelPath = OnnxEmbeddingModelPath,
            OnnxEmbeddingVocabPath = OnnxEmbeddingVocabPath,
        };

        if (!string.IsNullOrEmpty(ApiKey))
        {
            try
            {
                var plainBytes = System.Text.Encoding.UTF8.GetBytes(ApiKey);
                var cipherBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                toSave.EncryptedApiKey = Convert.ToBase64String(cipherBytes);
            }
            catch
            {
                // DPAPI unavailable (e.g. non-Windows) — store plaintext as
                // a last resort. Better than losing the key silently.
                toSave.EncryptedApiKey = null;
                toSave.ApiKey = ApiKey;
            }
        }

        var configPath = Path.Combine(configDir, "config.json");
        var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
    }
}
