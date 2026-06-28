using System.Runtime.CompilerServices;
using System.Text;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.Agent.Prompts;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent.Engine;

public sealed class AgentSession : IDisposable
{
    private readonly IChatService _chat;
    private readonly ToolRegistry _tools;
    private readonly SkillRegistry? _skills;
    private readonly IRetriever? _retriever;
    private readonly ILogger<AgentSession> _logger;
    private string? _projectDir;
    private readonly List<ChatMessage> _history = [];
    private readonly int _maxIterations = 10;
    private const int MaxHistoryMessages = 50;
    private const int DefaultMaxTokens = 4096;

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
    public event Action<string, string, TimeSpan>? OnToolResult;
    public event Action<string>? OnError;
    public event Action<int, int>? OnTokenUsage;
    public event Action<string, IReadOnlyList<RetrievalResult>>? OnRetrievalResults;

    public AgentSession(IChatService chat, ToolRegistry tools, string? projectDir, ILogger<AgentSession> logger,
        SkillRegistry? skills = null, IRetriever? retriever = null)
    {
        _chat = chat;
        _tools = tools;
        _skills = skills;
        _retriever = retriever;
        _projectDir = projectDir;
        _logger = logger;

        _tools.OnRetrievalResults += OnRetrievalResults;
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

    /// <summary>
    /// Checks if the user message starts with a registered slash command
    /// and returns the matching skill, or null.
    /// </summary>
    private ISkill? TryResolveSlashCommand(string userMessage)
    {
        if (_skills is null || !userMessage.StartsWith('/')) return null;
        var parts = userMessage.Split(' ', 2);
        var cmd = parts[0];
        return _skills.FindBySlashCommand(cmd);
    }

    /// <summary>
    /// Strips the slash command prefix from the user message, leaving
    /// the rest of the message for the LLM to process.
    /// </summary>
    private static string StripSlashCommand(string userMessage)
    {
        var parts = userMessage.Split(' ', 2);
        return parts.Length > 1 ? parts[1].Trim() : "";
    }

    public async IAsyncEnumerable<string> SendMessageAsync(string userMessage, [EnumeratorCancellation] CancellationToken ct = default)
    {
        // --- Skill activation ---
        var activeSkills = new List<ISkill>();
        var skillContextMessages = new List<ChatMessage>();
        var skillPromptAugment = new StringBuilder();

        if (_skills is not null)
        {
            // Check for explicit slash command first
            var slashSkill = TryResolveSlashCommand(userMessage);

            if (slashSkill is not null)
            {
                activeSkills.Add(slashSkill);
                // Strip the slash command from the user message
                userMessage = StripSlashCommand(userMessage);
            }

            // Then check for auto-activation (excluding already-triggered skills)
            var autoSkills = _skills.GetAutoActivatedSkills(userMessage, _projectDir)
                .Where(s => !activeSkills.Contains(s));
            activeSkills.AddRange(autoSkills);

            // Build prompt augmentation and pre-fetch context for all active skills
            foreach (var skill in activeSkills)
            {
                var augment = skill.GetPromptAugmentation();
                if (!string.IsNullOrEmpty(augment))
                {
                    skillPromptAugment.AppendLine(augment);
                    skillPromptAugment.AppendLine();
                }

                try
                {
                    var context = await skill.PreFetchContextAsync(userMessage, _retriever, ct);
                    skillContextMessages.AddRange(context);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Skill {SkillName} pre-fetch failed", skill.Name);
                }
            }
        }

        // Inject skill context as system messages before the user message
        foreach (var ctxMsg in skillContextMessages)
        {
            _history.Add(ctxMsg);
        }

        _history.Add(ChatMessage.User(userMessage));

        // If skills are active, inject the augmentation as a system message
        if (skillPromptAugment.Length > 0)
        {
            _history.Add(ChatMessage.System(skillPromptAugment.ToString()));
        }

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            TrimHistory();

            var request = new ChatRequest
            {
                Messages = _history.ToList(),
                Tools = _tools.GetDefinitions(),
                Temperature = 0.3,
                MaxTokens = DefaultMaxTokens
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

            // Estimate token usage (~4 chars ≈ 1 token heuristic) since
            // streaming responses don't include usage metadata from the API.
            var inputChars = _history.Sum(m => (m.Content?.Length ?? 0) + m.ToolCalls.Sum(tc => tc.Arguments.Length));
            var outputChars = content.Length + toolCalls.Sum(tc => tc.Arguments.Length);
            OnTokenUsage?.Invoke(inputChars / 4, outputChars / 4);

            _history.Add(new ChatMessage
            {
                Role = "assistant",
                Content = content,
                ToolCalls = toolCalls
            });

            if (toolCalls.Count > 0)
            {
                // Execute tools sequentially instead of in parallel. Several
                // tools (write_file, diff_apply, batch_edit, run_command) write
                // to the project directory, so parallel execution can race on
                // the same file and produce non-deterministic results. Sequential
                // execution preserves the order implied by the tool_calls list
                // and keeps the side-effect order predictable.
                foreach (var toolCall in toolCalls)
                {
                    string result;
                    try
                    {
                        OnToolCall?.Invoke(toolCall.Name, toolCall.Arguments);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        result = await _tools.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
                        sw.Stop();
                        OnToolResult?.Invoke(result, toolCall.Name, sw.Elapsed);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Tool {ToolName} threw unexpectedly", toolCall.Name);
                        result = $"Error: Tool '{toolCall.Name}' failed: {ex.Message}";
                        OnToolResult?.Invoke(result, toolCall.Name, TimeSpan.Zero);
                    }

                    _history.Add(ChatMessage.Tool(toolCall.Id, SanitizeToolResult(toolCall.Name, result)));
                }

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

    public void Dispose()
    {
        _tools.OnRetrievalResults -= OnRetrievalResults;
        _history.Clear();
    }

    public void SaveHistory(string path, CancellationToken ct = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (dir is not null) Directory.CreateDirectory(dir);
            var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var wrapper = new { Version = 1, Messages = _history };
            var json = System.Text.Json.JsonSerializer.Serialize(wrapper, opts);
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
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Try the versioned envelope format first.
            List<ChatMessage>? loaded = null;
            try
            {
                var wrapper = System.Text.Json.JsonSerializer.Deserialize<HistoryWrapper>(json, opts);
                if (wrapper?.Messages is { Count: > 0 })
                    loaded = wrapper.Messages;
            }
            catch
            {
                // Not the wrapper format — fall through to legacy deserialization.
            }

            // Fall back to legacy format (bare List<ChatMessage>) for backward compat.
            loaded ??= System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(json);

            if (loaded is { Count: > 0 })
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

    /// <summary>
    /// Wraps file-reading tool results with context markers to reduce
    /// prompt-injection risk from untrusted file/log contents.
    /// </summary>
    private static string SanitizeToolResult(string toolName, string result)
    {
        if (toolName is "read_file" or "list_files" or "parse_log" or "git_status")
            return $"[Tool output from {toolName}]\n{result}\n[End of tool output]";
        return result;
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

        // If a single message still exceeds the budget, truncate it
        var remaining = EstimateHistoryChars();
        if (remaining > MaxContextChars && _history.Count == 2)
        {
            var msg = _history[1];
            if (msg.Content is { Length: > 0 })
            {
                var maxChars = MaxContextChars - (EstimateHistoryChars() - msg.Content.Length);
                msg.Content = msg.Content[..Math.Max(0, maxChars)] + "\n[... truncated ...]";
            }
        }
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

    /// <summary>
    /// Versioned envelope for persisted chat history, enabling future format
    /// migrations without breaking backward compatibility with old files.
    /// </summary>
    private sealed class HistoryWrapper
    {
        public int Version { get; set; }
        public List<ChatMessage>? Messages { get; set; }
    }
}
