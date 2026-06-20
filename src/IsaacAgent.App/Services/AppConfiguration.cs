using IsaacAgent.LLM;

namespace IsaacAgent.App.Services;

public sealed class AppConfiguration
{
    public ProviderType ProviderType { get; set; } = ProviderType.OpenAICompatible;
    public string Endpoint { get; set; } = "https://api.minimax.chat/v1";
    public string Model { get; set; } = "abab6.5s-chat";
    public string? ApiKey { get; set; }

    public static AppConfiguration Load()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent",
            "config.json"
        );

        if (File.Exists(configPath))
        {
            var json = File.ReadAllText(configPath);
            return System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(json) ?? new();
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

        return new AppConfiguration();
    }

    public void Save()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent"
        );
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "config.json");
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(configPath, json);
    }
}
