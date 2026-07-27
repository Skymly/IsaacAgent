using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.Services;

/// <summary>
/// File-backed Chat session store: one manifest per project under an injectable root
/// (production: %APPDATA%/IsaacAgent/sessions/{projectHash}.json).
/// </summary>
public sealed class FileChatSessionStore : IChatSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _rootDirectory;
    private readonly ILogger<FileChatSessionStore>? _logger;

    public FileChatSessionStore(string rootDirectory, ILogger<FileChatSessionStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = rootDirectory;
        _logger = logger;
    }

    public async Task SaveAsync(string? projectDir, ProjectSessionManifest manifest, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var path = GetStorePath(projectDir);
        if (path is null)
            return;

        try
        {
            Directory.CreateDirectory(_rootDirectory);
            var toWrite = new ProjectSessionManifest
            {
                Version = manifest.Version == 0 ? 1 : manifest.Version,
                ProjectDir = projectDir ?? "",
                SavedAt = manifest.SavedAt == default ? DateTimeOffset.UtcNow : manifest.SavedAt,
                Tabs = manifest.Tabs
            };
            var json = JsonSerializer.Serialize(toWrite, JsonOptions);
            await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save chat session store to {Path}", path);
        }
    }

    public async Task<ProjectSessionManifest> LoadAsync(string? projectDir, CancellationToken ct = default)
    {
        var path = GetStorePath(projectDir);
        if (path is null)
            return EmptyManifest();

        try
        {
            if (!File.Exists(path))
                return EmptyManifest();

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<ProjectSessionManifest>(json, JsonOptions);
            if (loaded is null)
            {
                _logger?.LogWarning("Chat session store at {Path} deserialized to null; using empty session", path);
                return EmptyManifest();
            }

            loaded.Tabs ??= [];
            foreach (var tab in loaded.Tabs)
                tab.Messages ??= [];

            return loaded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load chat session store from {Path}; using empty session", path);
            return EmptyManifest();
        }
    }

    /// <summary>
    /// Resolves the manifest path for a project, or null when no project is open.
    /// </summary>
    public string? GetStorePath(string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            return null;

        return Path.Combine(_rootDirectory, $"{ComputeProjectHash(projectDir)}.json");
    }

    /// <summary>
    /// Stable 12-hex SHA256 of the lowercased project path (matches legacy history/ hashing).
    /// </summary>
    public static string ComputeProjectHash(string projectDir)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(projectDir.ToLowerInvariant()));
        return Convert.ToHexString(hashBytes)[..12];
    }

    private static ProjectSessionManifest EmptyManifest() => new();
}
