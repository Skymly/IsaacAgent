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
                foreach (var toolCall in toolCalls)
                {
                    OnToolCall?.Invoke(toolCall.Name, toolCall.Arguments);
                    var result = await _tools.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
                    OnToolResult?.Invoke(result);
                    _history.Add(ChatMessage.Tool(toolCall.Id, result));
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

    private sealed class StreamedToolCall
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public System.Text.StringBuilder Arguments { get; } = new();
    }
}
