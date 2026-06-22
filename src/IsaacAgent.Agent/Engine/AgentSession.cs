using System.Runtime.CompilerServices;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.Agent.Prompts;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent.Engine;

public sealed class AgentSession
{
    private readonly IChatService _chat;
    private readonly ToolRegistry _tools;
    private readonly ILogger<AgentSession> _logger;
    private string? _projectDir;
    private readonly List<ChatMessage> _history = [];
    private readonly int _maxIterations = 10;
    private const int MaxHistoryMessages = 50;

    public event Action<string>? OnTextGenerated;
    public event Action<string, string>? OnToolCall;
    public event Action<string>? OnToolResult;
    public event Action<string>? OnError;

    public AgentSession(IChatService chat, ToolRegistry tools, string? projectDir, ILogger<AgentSession> logger)
    {
        _chat = chat;
        _tools = tools;
        _projectDir = projectDir;
        _logger = logger;

        _tools.ReconfigureForProject(projectDir);
        _history.Add(ChatMessage.System(SystemPrompts.BuildSystemPrompt(projectDir)));
    }

    public List<ChatMessage> History => _history;

    public void SetProjectDirectory(string? projectDir)
    {
        _projectDir = projectDir;
        _tools.ReconfigureForProject(projectDir);
        ClearHistory();
    }

    public async IAsyncEnumerable<string> SendMessageAsync(string userMessage, [EnumeratorCancellation] CancellationToken ct = default)
    {
        _history.Add(ChatMessage.User(userMessage));

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            TrimHistory();

            var request = new ChatRequest
            {
                Messages = _history.ToList(),
                Tools = _tools.GetDefinitions(),
                Temperature = 0.3,
                MaxTokens = 4096
            };

            var contentBuilder = new System.Text.StringBuilder();
            var toolCallAccumulator = new Dictionary<int, StreamedToolCall>();

            // Note: cannot yield inside try-catch, so let exceptions propagate
            // to the caller (ChatViewModel) which handles them.
            await foreach (var chunk in _chat.StreamAsync(request, ct))
            {
                if (chunk.IsToolCall)
                {
                    if (!toolCallAccumulator.TryGetValue(chunk.ToolCallIndex, out var tc))
                    {
                        tc = new StreamedToolCall();
                        toolCallAccumulator[chunk.ToolCallIndex] = tc;
                    }
                    if (chunk.ToolCallId is not null) tc.Id = chunk.ToolCallId;
                    if (chunk.ToolCallName is not null) tc.Name = chunk.ToolCallName;
                    if (chunk.ToolCallArguments is not null) tc.Arguments.Append(chunk.ToolCallArguments);
                }
                else if (!string.IsNullOrEmpty(chunk.Delta))
                {
                    contentBuilder.Append(chunk.Delta);
                    OnTextGenerated?.Invoke(chunk.Delta);
                    yield return chunk.Delta;
                }
            }

            var toolCalls = toolCallAccumulator.OrderBy(kv => kv.Key)
                .Select(kv =>
                {
                    var tc = kv.Value;
                    return new ToolCall
                    {
                        Id = tc.Id ?? $"call_{Guid.NewGuid():N}",
                        Name = tc.Name ?? "",
                        Arguments = tc.Arguments.ToString()
                    };
                }).ToList();

            var content = contentBuilder.ToString();
            _history.Add(new ChatMessage
            {
                Role = "assistant",
                Content = content,
                ToolCalls = toolCalls
            });

            if (toolCalls.Count > 0)
            {
                var toolTasks = toolCalls.Select(async toolCall =>
                {
                    try
                    {
                        OnToolCall?.Invoke(toolCall.Name, toolCall.Arguments);
                        var result = await _tools.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
                        OnToolResult?.Invoke(result);
                        return (toolCall, result);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Tool {ToolName} threw unexpectedly", toolCall.Name);
                        var errMsg = $"Error: Tool '{toolCall.Name}' failed: {ex.Message}";
                        OnToolResult?.Invoke(errMsg);
                        return (toolCall, errMsg);
                    }
                }).ToList();

                var results = await Task.WhenAll(toolTasks);
                foreach (var (toolCall, result) in results)
                    _history.Add(ChatMessage.Tool(toolCall.Id, result));
                continue;
            }

            yield break;
        }

        OnError?.Invoke("Max iterations reached.");
        yield return "[Error: Max tool call iterations reached. Please simplify your request.]";
    }

    public void ClearHistory()
    {
        _history.Clear();
        _history.Add(ChatMessage.System(SystemPrompts.BuildSystemPrompt(_projectDir)));
    }

    public void SaveHistory(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Directory.CreateDirectory(dir);
            var json = System.Text.Json.JsonSerializer.Serialize(_history, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save chat history to {Path}", path);
        }
    }

    public void LoadHistory(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var loaded = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(json);
            if (loaded is not null && loaded.Count > 0)
            {
                _history.Clear();
                _history.AddRange(loaded);
                _logger.LogInformation("Loaded {Count} messages from history file", loaded.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat history from {Path}", path);
        }
    }

    private void TrimHistory()
    {
        if (_history.Count <= MaxHistoryMessages)
            return;

        // Always keep the system prompt (index 0) and the most recent messages.
        // Remove oldest non-system messages until we're under the limit.
        var toRemove = _history.Count - MaxHistoryMessages;

        // Don't cut in the middle of a tool call / tool result pair.
        // If the message at index 1 is a "tool" result (orphaned without its
        // preceding assistant tool_calls message), remove it too.
        while (toRemove < _history.Count - 1 &&
               _history[1].Role == "tool")
        {
            toRemove++;
        }

        // Remove from index 1 onward (preserve system prompt at 0)
        _history.RemoveRange(1, toRemove);
        _logger.LogInformation("Trimmed {Count} old messages from history", toRemove);
    }

    private sealed class StreamedToolCall
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public System.Text.StringBuilder Arguments { get; } = new();
    }
}
