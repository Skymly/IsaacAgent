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

    /// <summary>
    /// Soft character budget for the conversation history (excluding the
    /// system prompt). When exceeded, oldest non-system messages are
    /// trimmed. Uses a rough ~4 chars ≈ 1 token heuristic, so 120k chars
    /// ≈ 30k tokens — leaving headroom for the response in typical
    /// 32k–128k context windows.
    /// </summary>
    private const int MaxContextChars = 120_000;

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
        // Two independent budgets:
        //  - MaxHistoryMessages: prevents unbounded message count (memory)
        //  - MaxContextChars: prevents context-window overflow from large
        //    tool results (e.g., list_files returning thousands of lines)
        // Trim if EITHER budget is exceeded.
        var charCount = EstimateHistoryChars();
        if (_history.Count <= MaxHistoryMessages && charCount <= MaxContextChars)
            return;

        // Always keep the system prompt (index 0) and the most recent messages.
        // Remove oldest non-system messages until we're under both limits.
        var toRemove = _history.Count - MaxHistoryMessages;
        if (toRemove < 1) toRemove = 1; // at least 1 if char budget exceeded

        // Extend the removal window to avoid orphaned tool-related messages:
        // 1. If the new first message (after removal) is a "tool" result without
        //    its preceding assistant tool_calls message, remove it too.
        // 2. If the new first message is an assistant message with tool_calls
        //    but its corresponding tool results would be removed, keep removing
        //    until we're past the entire tool_calls + tool_results group.
        while (toRemove < _history.Count - 1)
        {
            var newFirst = _history[toRemove + 1];

            // Case 1: orphaned tool result
            if (newFirst.Role == "tool")
            {
                toRemove++;
                continue;
            }

            // Case 2: assistant with tool_calls but no matching tool results
            if (newFirst.Role == "assistant" && newFirst.ToolCalls.Count > 0)
            {
                // Count how many tool results follow this assistant message
                var expectedResults = newFirst.ToolCalls.Count;
                var availableResults = 0;
                for (var i = toRemove + 2; i < _history.Count && availableResults < expectedResults; i++)
                {
                    if (_history[i].Role == "tool")
                        availableResults++;
                    else
                        break;
                }

                // If not all tool results fit within the kept window, remove the
                // entire group (assistant tool_calls + partial tool results).
                if (availableResults < expectedResults)
                {
                    toRemove++;
                    // Also skip the partial tool results that would be orphaned
                    while (toRemove < _history.Count - 1 && _history[toRemove + 1].Role == "tool")
                        toRemove++;
                    continue;
                }
            }

            break;
        }

        // If we're still over the char budget after the orphan-safe removal,
        // keep removing oldest messages (still respecting orphan rules) until
        // we're under budget or only the system prompt + last message remain.
        while (toRemove < _history.Count - 2 && EstimateHistoryChars(toRemove) > MaxContextChars)
        {
            var newFirst = _history[toRemove + 1];
            if (newFirst.Role == "tool")
            {
                toRemove++;
                continue;
            }
            if (newFirst.Role == "assistant" && newFirst.ToolCalls.Count > 0)
            {
                var expected = newFirst.ToolCalls.Count;
                var available = 0;
                for (var i = toRemove + 2; i < _history.Count && available < expected; i++)
                {
                    if (_history[i].Role == "tool") available++;
                    else break;
                }
                if (available < expected)
                {
                    toRemove++;
                    while (toRemove < _history.Count - 1 && _history[toRemove + 1].Role == "tool")
                        toRemove++;
                    continue;
                }
            }
            toRemove++;
        }

        // Remove from index 1 onward (preserve system prompt at 0)
        _history.RemoveRange(1, toRemove);
        _logger.LogInformation("Trimmed {Count} old messages from history ({Chars}→{Remaining} chars est.)",
            toRemove, charCount, EstimateHistoryChars());
    }

    /// <summary>
    /// Estimates total character count of all history messages (excluding
    /// the system prompt at index 0). Uses content + tool call names/args
    /// as a rough proxy for token consumption (~4 chars ≈ 1 token).
    /// </summary>
    private int EstimateHistoryChars(int skipCount = 0)
    {
        var total = 0;
        for (var i = 1 + skipCount; i < _history.Count; i++)
        {
            var msg = _history[i];
            total += msg.Content?.Length ?? 0;
            foreach (var tc in msg.ToolCalls)
            {
                total += tc.Name?.Length ?? 0;
                total += tc.Arguments?.Length ?? 0;
            }
        }
        return total;
    }

    private sealed class StreamedToolCall
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public System.Text.StringBuilder Arguments { get; } = new();
    }
}
