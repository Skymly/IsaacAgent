namespace IsaacAgent.Core.Models;

public sealed class ChatMessage
{
    public required string Role { get; init; }
    public required string Content { get; set; }
    public List<ToolCall> ToolCalls { get; set; } = [];
    public string? ToolCallId { get; set; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static ChatMessage System(string content) => new() { Role = "system", Content = content };
    public static ChatMessage User(string content) => new() { Role = "user", Content = content };
    public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
    public static ChatMessage Tool(string toolCallId, string content) => new() { Role = "tool", Content = content, ToolCallId = toolCallId };
}

public sealed class ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; }
}

public sealed class ToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required ToolParameters Parameters { get; init; }
}

public sealed class ToolParameters
{
    public string Type { get; init; } = "object";
    public Dictionary<string, ToolParameterProperty> Properties { get; init; } = [];
    public List<string> Required { get; init; } = [];
}

public sealed class ToolParameterProperty
{
    public required string Type { get; init; }
    public string? Description { get; init; }
    public List<string>? Enum { get; init; }
}
