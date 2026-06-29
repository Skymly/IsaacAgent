using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IsaacAgent.App.ViewModels;

namespace IsaacAgent.App.Services;

/// <summary>
///   Serializable representation of a chat message for persistence.
/// </summary>
public sealed class ChatMessageRecord
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public string ToolName { get; set; } = "";
    public bool IsToolCall { get; set; }
    public bool IsToolResult { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
///   Serializable representation of a chat tab for persistence.
/// </summary>
public sealed class ChatTabRecord
{
    public string Title { get; set; } = "";
    public List<ChatMessageRecord> Messages { get; set; } = [];
}

/// <summary>
///   Serializable representation of a chat session for persistence.
/// </summary>
public sealed class ChatSessionRecord
{
    public string ProjectDir { get; set; } = "";
    public List<ChatTabRecord> Tabs { get; set; } = [];
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
///   Manages chat history persistence, export, and search.
///   History is stored as JSON files per project directory.
/// </summary>
public sealed class ChatHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///   Get the history file path for a given project directory.
    ///   Returns null if projectDir is null or empty.
    /// </summary>
    public static string? GetHistoryPath(string? projectDir)
    {
        if (string.IsNullOrEmpty(projectDir)) return null;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent", "chat-history");
        var safeName = SanitizeFileName(projectDir);
        return Path.Combine(dir, $"{safeName}.json");
    }

    /// <summary>
    ///   Save the current chat session (all tabs and messages) for a project.
    /// </summary>
    public void SaveSession(string? projectDir, ChatViewModel chat)
    {
        var path = GetHistoryPath(projectDir);
        if (path is null) return;

        var session = new ChatSessionRecord
        {
            ProjectDir = projectDir ?? "",
            SavedAt = DateTimeOffset.UtcNow,
            Tabs = chat.Tabs.Select(t => new ChatTabRecord
            {
                Title = t.Title,
                Messages = t.Messages.Select(m => new ChatMessageRecord
                {
                    Role = m.Role,
                    Content = m.Content,
                    ToolName = m.ToolName,
                    IsToolCall = m.IsToolCall,
                    IsToolResult = m.IsToolResult
                }).ToList()
            }).ToList()
        };

        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    ///   Load a saved chat session for a project. Returns null if no history exists.
    /// </summary>
    public ChatSessionRecord? LoadSession(string? projectDir)
    {
        var path = GetHistoryPath(projectDir);
        if (path is null || !File.Exists(path)) return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ChatSessionRecord>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///   Restore chat tabs and messages from a saved session.
    ///   Returns true if history was restored, false otherwise.
    /// </summary>
    public bool RestoreSession(string? projectDir, ChatViewModel chat)
    {
        var session = LoadSession(projectDir);
        if (session is null || session.Tabs.Count == 0) return false;

        // Clear existing tabs and restore from history.
        while (chat.Tabs.Count > 0)
        {
            var tab = chat.Tabs[0];
            tab.Dispose();
            chat.Tabs.Remove(tab);
        }

        foreach (var tabRecord in session.Tabs)
        {
            chat.AddTabCommand.Execute(null);
            var tab = chat.Tabs[^1];
            tab.Title = tabRecord.Title;
            foreach (var msg in tabRecord.Messages)
            {
                tab.Messages.Add(new ChatMessageViewModel
                {
                    Role = msg.Role,
                    Content = msg.Content,
                    ToolName = msg.ToolName,
                    IsToolCall = msg.IsToolCall,
                    IsToolResult = msg.IsToolResult
                });
            }
        }

        return true;
    }

    /// <summary>
    ///   Export a chat tab's messages to a Markdown string.
    /// </summary>
    public static string ExportToMarkdown(ChatTabViewModel tab)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {tab.Title}");
        sb.AppendLine();
        sb.AppendLine($"Exported: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        foreach (var msg in tab.Messages)
        {
            var label = msg.Role switch
            {
                "user" => "User",
                "assistant" => "Assistant",
                "tool" => $"Tool: {msg.ToolName}",
                "tool_result" => $"Tool Result: {msg.ToolName}",
                "retrieval" => "Knowledge Retrieved",
                "error" => "Error",
                "system" => "System",
                _ => msg.Role
            };
            sb.AppendLine($"## {label}");
            sb.AppendLine();
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///   Export a chat tab's messages to a JSON string.
    /// </summary>
    public static string ExportToJson(ChatTabViewModel tab)
    {
        var record = new ChatTabRecord
        {
            Title = tab.Title,
            Messages = tab.Messages.Select(m => new ChatMessageRecord
            {
                Role = m.Role,
                Content = m.Content,
                ToolName = m.ToolName,
                IsToolCall = m.IsToolCall,
                IsToolResult = m.IsToolResult
            }).ToList()
        };
        return JsonSerializer.Serialize(record, JsonOptions);
    }

    /// <summary>
    ///   Search messages across all tabs for a query string.
    ///   Returns a list of (tab title, message) pairs that match.
    /// </summary>
    public static List<(string TabTitle, ChatMessageViewModel Message)> SearchMessages(
        ChatViewModel chat, string query)
    {
        var results = new List<(string TabTitle, ChatMessageViewModel Message)>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        var lowerQuery = query.ToLowerInvariant();
        foreach (var tab in chat.Tabs)
        {
            foreach (var msg in tab.Messages)
            {
                if (msg.Content.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add((tab.Title, msg));
                }
            }
        }
        return results;
    }

    /// <summary>
    ///   Delete the history file for a project.
    /// </summary>
    public void DeleteSession(string? projectDir)
    {
        var path = GetHistoryPath(projectDir);
        if (path is not null && File.Exists(path))
            File.Delete(path);
    }

    private static string SanitizeFileName(string path)
    {
        // Replace invalid filename characters with underscores.
        var invalid = Path.GetInvalidFileNameChars();
        var result = new StringBuilder(path.Length);
        foreach (var c in path)
            result.Append(invalid.Contains(c) ? '_' : c);
        // Also replace colons and backslashes for cleaner filenames.
        return result.ToString().Replace(':', '_').Replace('\\', '_').Replace('/', '_');
    }
}
