using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IsaacAgent.Core.Models;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.Services;

/// <summary>
/// File-backed Chat session store: one manifest per project under an injectable root
/// (production: %APPDATA%/IsaacAgent/sessions/{projectHash}.json).
/// On first load with no sessions file, migrates once from legacy history/ + chat-history/.
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
    private readonly string _historyRootDirectory;
    private readonly string _chatHistoryRootDirectory;
    private readonly ILogger<FileChatSessionStore>? _logger;

    public FileChatSessionStore(
        string rootDirectory,
        string historyRootDirectory,
        string chatHistoryRootDirectory,
        ILogger<FileChatSessionStore>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(historyRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(chatHistoryRootDirectory);
        _rootDirectory = rootDirectory;
        _historyRootDirectory = historyRootDirectory;
        _chatHistoryRootDirectory = chatHistoryRootDirectory;
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
            if (File.Exists(path))
            {
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

            // First open with no sessions file: migrate from legacy (if any), then write
            // sessions/ so subsequent loads treat it as sole authority (including empty).
            var migrated = await BuildMigratedManifestAsync(projectDir!, ct).ConfigureAwait(false);
            migrated.ProjectDir = projectDir!;
            migrated.SavedAt = DateTimeOffset.UtcNow;
            await SaveAsync(projectDir, migrated, ct).ConfigureAwait(false);
            if (migrated.Tabs.Count > 0)
            {
                _logger?.LogInformation(
                    "Migrated chat session for project {ProjectDir} into {Path} ({TabCount} tabs)",
                    projectDir,
                    path,
                    migrated.Tabs.Count);
            }

            return migrated;
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

    private async Task<ProjectSessionManifest> BuildMigratedManifestAsync(
        string projectDir,
        CancellationToken ct)
    {
        var historyTabs = await LoadLegacyHistoryTabsAsync(projectDir, ct).ConfigureAwait(false);
        var chatSession = await LoadLegacyChatHistoryAsync(projectDir, ct).ConfigureAwait(false);
        var chatTabs = chatSession?.Tabs;

        if (chatTabs is { Count: > 0 })
        {
            var count = Math.Max(chatTabs.Count, historyTabs.Count);
            var tabs = new List<SessionTabRecord>(count);
            for (var i = 0; i < count; i++)
            {
                var title = i < chatTabs.Count && !string.IsNullOrWhiteSpace(chatTabs[i].Title)
                    ? chatTabs[i].Title
                    : DefaultTabTitle(i);

                List<ChatMessage> messages;
                var historyVersion = 1;
                if (i < historyTabs.Count)
                {
                    messages = historyTabs[i].Messages;
                    historyVersion = historyTabs[i].Version;
                }
                else
                {
                    messages = ConvertUiMessages(chatTabs[i].Messages);
                }

                tabs.Add(new SessionTabRecord
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    HistoryVersion = historyVersion,
                    Messages = messages
                });
            }

            return new ProjectSessionManifest { Tabs = tabs };
        }

        if (historyTabs.Count > 0)
        {
            return new ProjectSessionManifest
            {
                Tabs = historyTabs.Select((h, i) => new SessionTabRecord
                {
                    Id = Guid.NewGuid(),
                    Title = DefaultTabTitle(i),
                    HistoryVersion = h.Version,
                    Messages = h.Messages
                }).ToList()
            };
        }

        return EmptyManifest();
    }

    private async Task<List<LegacyHistoryTab>> LoadLegacyHistoryTabsAsync(
        string projectDir,
        CancellationToken ct)
    {
        var result = new List<LegacyHistoryTab>();
        if (!Directory.Exists(_historyRootDirectory))
            return result;

        var hash = ComputeProjectHash(projectDir);
        var prefix = $"project_{hash}_";
        // Prefer LastWriteTime as a proxy for tab creation order (ids are random hex).
        var files = Directory.GetFiles(_historyRootDirectory, $"{prefix}*.json")
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.LastWriteTimeUtc)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file.FullName, ct).ConfigureAwait(false);
                var envelope = TryParseHistoryEnvelope(json);
                if (envelope is null)
                    continue;

                result.Add(envelope);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Skipping unreadable legacy history file {Path}", file.FullName);
            }
        }

        return result;
    }

    private async Task<ChatSessionRecord?> LoadLegacyChatHistoryAsync(
        string projectDir,
        CancellationToken ct)
    {
        var path = GetLegacyChatHistoryPath(projectDir);
        if (path is null || !File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ChatSessionRecord>(json, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read legacy chat-history at {Path}", path);
            return null;
        }
    }

    private string? GetLegacyChatHistoryPath(string? projectDir)
    {
        if (string.IsNullOrEmpty(projectDir))
            return null;

        return Path.Combine(_chatHistoryRootDirectory, $"{SanitizeFileName(projectDir)}.json");
    }

    private static LegacyHistoryTab? TryParseHistoryEnvelope(string json)
    {
        try
        {
            var wrapper = JsonSerializer.Deserialize<LegacyHistoryEnvelope>(json, JsonOptions);
            if (wrapper?.Messages is { Count: > 0 })
            {
                return new LegacyHistoryTab(
                    wrapper.Version == 0 ? 1 : wrapper.Version,
                    wrapper.Messages);
            }
        }
        catch
        {
            // fall through to bare list
        }

        try
        {
            var bare = JsonSerializer.Deserialize<List<ChatMessage>>(json, JsonOptions);
            if (bare is { Count: > 0 })
                return new LegacyHistoryTab(1, bare);
        }
        catch
        {
            // unreadable
        }

        return null;
    }

    private static List<ChatMessage> ConvertUiMessages(List<ChatMessageRecord>? records)
    {
        if (records is null || records.Count == 0)
            return [];

        var messages = new List<ChatMessage>(records.Count);
        foreach (var record in records)
        {
            var role = string.IsNullOrWhiteSpace(record.Role) ? "user" : record.Role;
            messages.Add(new ChatMessage
            {
                Role = role,
                Content = record.Content ?? ""
            });
        }

        return messages;
    }

    private static string DefaultTabTitle(int index) =>
        index == 0 ? "Chat" : $"Chat {index + 1}";

    private static string SanitizeFileName(string path)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new StringBuilder(path.Length);
        foreach (var c in path)
            result.Append(invalid.Contains(c) ? '_' : c);

        return result.ToString()
            .Replace(':', '_')
            .Replace('\\', '_')
            .Replace('/', '_');
    }

    private static ProjectSessionManifest EmptyManifest() => new();

    private sealed class LegacyHistoryEnvelope
    {
        public int Version { get; set; }
        public List<ChatMessage>? Messages { get; set; }
    }

    private sealed record LegacyHistoryTab(int Version, List<ChatMessage> Messages);
}
