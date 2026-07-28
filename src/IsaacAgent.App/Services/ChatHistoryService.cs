using System.Text.Json;
using System.Text.Json.Serialization;
using IsaacAgent.App.ViewModels;

namespace IsaacAgent.App.Services;

/// <summary>
///   Serializable representation of a chat message for export and legacy
///   chat-history/ deserialization during Chat session store migration.
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
///   Serializable representation of a chat tab for export and legacy migration.
/// </summary>
public sealed class ChatTabRecord
{
    public string Title { get; set; } = "";
    public List<ChatMessageRecord> Messages { get; set; } = [];
}

/// <summary>
///   Serializable representation of a legacy multi-tab chat-history/ session.
///   Used only when migrating into the Chat session store; not an authoritative path.
/// </summary>
public sealed class ChatSessionRecord
{
    public string ProjectDir { get; set; } = "";
    public List<ChatTabRecord> Tabs { get; set; } = [];
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
///   Export and in-memory search helpers over the live chat UI.
///   Project persistence is owned by <see cref="IChatSessionStore"/>; this type
///   does not read or write chat-history/ or history/ on disk.
/// </summary>
public static class ChatHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
    ///   Search messages across all open tabs for a query string.
    ///   Operates on the live UI only; does not read the on-disk store.
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
}
