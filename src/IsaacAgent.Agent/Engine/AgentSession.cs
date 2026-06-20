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
    private readonly string? _projectDir;
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

        _history.Add(ChatMessage.System(SystemPrompts.BuildSystemPrompt(projectDir)));
    }

    public List<ChatMessage> History => _history;

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

            ChatResponse response = null!;
            string? errorMessage = null;
            try
            {
                response = await _chat.CompleteAsync(request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat completion failed");
                OnError?.Invoke(ex.Message);
                errorMessage = ex.Message;
            }

            if (errorMessage is not null)
            {
                yield return $"[Error: {errorMessage}]";
                yield break;
            }

            _history.Add(response.Message);

            if (response.Message.ToolCalls.Count > 0)
            {
                foreach (var toolCall in response.Message.ToolCalls)
                {
                    OnToolCall?.Invoke(toolCall.Name, toolCall.Arguments);

                    var result = await _tools.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
                    OnToolResult?.Invoke(result);

                    _history.Add(ChatMessage.Tool(toolCall.Id, result));
                }

                continue;
            }

            if (!string.IsNullOrEmpty(response.Message.Content))
            {
                OnTextGenerated?.Invoke(response.Message.Content);
                yield return response.Message.Content;
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
}
