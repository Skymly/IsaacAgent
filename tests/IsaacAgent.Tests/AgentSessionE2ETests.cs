using System.Runtime.CompilerServices;
using IsaacAgent.Agent.Engine;
using IsaacAgent.Agent.Skills;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// End-to-end tests for the AgentSession orchestration loop using a
/// scripted fake LLM and verifiable fake tools. These tests exercise the
/// full multi-iteration flow (tool call → execution → result feedback →
/// continuation) without any real LLM endpoint or Ollama dependency.
/// </summary>
public class AgentSessionE2ETests
{
    // ── Test doubles ──────────────────────────────────────────────

    /// <summary>
    /// A scripted chat service that returns a different batch of chunks
    /// per StreamAsync call (i.e. per agent iteration). This lets tests
    /// script multi-turn conversations: "iteration 0: tool call, iteration
    /// 1: final text".
    /// </summary>
    private sealed class ScriptedChatService : IChatService
    {
        private readonly List<List<ChatChunk>> _turns;
        private int _callIndex;

        public int CallCount => _callIndex;
        public List<ChatRequest> ReceivedRequests { get; } = [];

        public ScriptedChatService(params List<ChatChunk>[] turns) => _turns = [.. turns];

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse
            {
                Message = new ChatMessage { Role = "assistant", Content = "ok" }
            });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ReceivedRequests.Add(request);
            await Task.Yield();
            var turn = _callIndex < _turns.Count ? _turns[_callIndex] : _turns[^1];
            _callIndex++;
            foreach (var chunk in turn)
                yield return chunk;
        }
    }

    /// <summary>
    /// A verifiable fake tool that records every invocation and returns a
    /// configurable result. Lets tests assert that the agent passed the
    /// correct arguments and that the tool result was fed back to the LLM.
    /// </summary>
    private sealed class FakeTool : ITool
    {
        public string Name { get; }
        public string Description { get; }
        public ToolDefinition Definition { get; }
        public string Result { get; set; } = "ok";

        public List<(string Name, string Arguments)> Invocations { get; } = [];

        public FakeTool(string name, string result = "ok", string description = "test tool")
        {
            Name = name;
            Description = description;
            Result = result;
            Definition = new ToolDefinition
            {
                Name = name,
                Description = description,
                Parameters = new ToolParameters()
            };
        }

        public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
        {
            Invocations.Add((Name, arguments));
            return Task.FromResult(Result);
        }
    }

    /// <summary>
    /// A fake tool that throws on execution, to test error handling in the
    /// agent loop.
    /// </summary>
    private sealed class ThrowingTool : ITool
    {
        public string Name => "test_throwing";
        public string Description => "always throws";
        public ToolDefinition Definition { get; } = new()
        {
            Name = "test_throwing",
            Description = "always throws",
            Parameters = new ToolParameters()
        };

        public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static AgentSession CreateSession(
        IChatService chat,
        IEnumerable<ITool>? tools = null,
        SkillRegistry? skills = null)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(toolLogger);
        // AgentSession ctor calls ReconfigureForProject(null) which
        // clears the registry and re-registers built-in tools. So we
        // register our fake tools AFTER construction. Using unique
        // "test_*" names avoids collisions with built-in tools.
        var session = new AgentSession(chat, registry, null, logger, skills);
        if (tools is not null)
            registry.RegisterAll(tools);
        return session;
    }

    private static ChatChunk TextChunk(string text) => new(text, false, -1, null, null, null);

    private static ChatChunk ToolCallChunk(int index, string id, string name, string args)
        => new("", true, index, id, name, args);

    private static ChatChunk ToolCallEnd(int index)
        => new("", true, index, null, null, null);

    // ── Tests: multi-turn tool chain ──────────────────────────────

    [Fact]
    public async Task E2E_SingleToolCall_ThenFinalReply()
    {
        var echo = new FakeTool("test_echo", "echo-result");
        var chat = new ScriptedChatService(
            // Iteration 0: LLM calls test_echo
            [ToolCallChunk(0, "call_1", "test_echo", """{"msg":"hello"}"""), ToolCallEnd(0)],
            // Iteration 1: LLM produces final text
            [TextChunk("The echo returned: echo-result")]
        );

        var session = CreateSession(chat, [echo]);
        var output = new List<string>();
        await foreach (var c in session.SendMessageAsync("echo hello"))
            output.Add(c);

        // Tool was called once with correct args
        Assert.Single(echo.Invocations);
        Assert.Equal("test_echo", echo.Invocations[0].Name);
        Assert.Contains("hello", echo.Invocations[0].Arguments);

        // Final text was streamed
        Assert.Contains("The echo returned: echo-result", output);

        // LLM was called twice (tool call turn + final turn)
        Assert.Equal(2, chat.CallCount);

        // History: system + user + assistant(tool) + tool_result + assistant(text)
        Assert.Equal("tool", session.History[3].Role);
        Assert.Contains("echo-result", session.History[3].Content);
    }

    [Fact]
    public async Task E2E_ToolChain_ACallsB_ThenFinalReply()
    {
        // Simulates: LLM calls tool A, gets result, then calls tool B,
        // gets result, then produces final text.
        var toolA = new FakeTool("test_step_a", "step-a-output");
        var toolB = new FakeTool("test_step_b", "step-b-output");
        var chat = new ScriptedChatService(
            // Iteration 0: call A
            [ToolCallChunk(0, "c1", "test_step_a", """{"n":1}"""), ToolCallEnd(0)],
            // Iteration 1: call B (using A's result)
            [ToolCallChunk(0, "c2", "test_step_b", """{"n":2}"""), ToolCallEnd(0)],
            // Iteration 2: final text
            [TextChunk("Done: step-b-output")]
        );

        var session = CreateSession(chat, [toolA, toolB]);
        var output = new List<string>();
        await foreach (var c in session.SendMessageAsync("run chain"))
            output.Add(c);

        Assert.Single(toolA.Invocations);
        Assert.Single(toolB.Invocations);
        Assert.Equal(3, chat.CallCount);
        Assert.Contains("Done: step-b-output", output);

        // History: system + user + asst(A) + tool(A) + asst(B) + tool(B) + asst(text)
        Assert.Equal("tool", session.History[3].Role);
        Assert.Contains("step-a-output", session.History[3].Content);
        Assert.Equal("tool", session.History[5].Role);
        Assert.Contains("step-b-output", session.History[5].Content);
    }

    [Fact]
    public async Task E2E_ParallelToolCalls_BothExecutedSequentially()
    {
        var toolA = new FakeTool("test_par_a", "par-a");
        var toolB = new FakeTool("test_par_b", "par-b");
        var chat = new ScriptedChatService(
            // Iteration 0: LLM emits two tool calls in one response
            [
                ToolCallChunk(0, "c1", "test_par_a", """{"x":1}"""),
                ToolCallChunk(1, "c2", "test_par_b", """{"x":2}"""),
            ],
            // Iteration 1: final text
            [TextChunk("Both done")]
        );

        var session = CreateSession(chat, [toolA, toolB]);
        var output = new List<string>();
        await foreach (var c in session.SendMessageAsync("parallel"))
            output.Add(c);

        Assert.Single(toolA.Invocations);
        Assert.Single(toolB.Invocations);
        // A executed before B (sequential, index order)
        Assert.Equal("test_par_a", toolA.Invocations[0].Name);
        Assert.Equal("test_par_b", toolB.Invocations[0].Name);
        Assert.Contains("Both done", output);
    }

    // ── Tests: tool result sanitization ───────────────────────────

    [Fact]
    public async Task E2E_ReadFileResult_IsWrappedWithBoundaryMarkers()
    {
        // read_file is one of the tools whose results get sanitized with
        // boundary markers to prevent LLM injection.
        var readFile = new FakeTool("read_file", "file contents here");
        var chat = new ScriptedChatService(
            [ToolCallChunk(0, "c1", "read_file", """{"path":"test.lua"}"""), ToolCallEnd(0)],
            [TextChunk("ok")]
        );

        var session = CreateSession(chat, [readFile]);
        await foreach (var _ in session.SendMessageAsync("read test.lua")) { }

        var toolMsg = session.History.First(h => h.Role == "tool");
        Assert.Contains("[Tool output from read_file]", toolMsg.Content);
        Assert.Contains("[End of tool output]", toolMsg.Content);
        Assert.Contains("file contents here", toolMsg.Content);
    }

    [Fact]
    public async Task E2E_NonSanitizedTool_ResultNotWrapped()
    {
        // test_echo is not in the sanitization list — result should be raw.
        var echo = new FakeTool("test_echo", "raw result");
        var chat = new ScriptedChatService(
            [ToolCallChunk(0, "c1", "test_echo", """{}"""), ToolCallEnd(0)],
            [TextChunk("ok")]
        );

        var session = CreateSession(chat, [echo]);
        await foreach (var _ in session.SendMessageAsync("echo")) { }

        var toolMsg = session.History.First(h => h.Role == "tool");
        Assert.DoesNotContain("[Tool output from", toolMsg.Content);
        Assert.Equal("raw result", toolMsg.Content);
    }

    // ── Tests: tool error handling ────────────────────────────────

    [Fact]
    public async Task E2E_ThrowingTool_ErrorFedBackToLLM()
    {
        var chat = new ScriptedChatService(
            [ToolCallChunk(0, "c1", "test_throwing", """{}"""), ToolCallEnd(0)],
            [TextChunk("I see the tool failed")]
        );

        var session = CreateSession(chat, [new ThrowingTool()]);
        var output = new List<string>();
        await foreach (var c in session.SendMessageAsync("call throwing tool"))
            output.Add(c);

        // The error message should be in the tool result. ToolRegistry
        // catches the exception and returns "Error executing ...: boom".
        var toolMsg = session.History.First(h => h.Role == "tool");
        Assert.Contains("boom", toolMsg.Content);
        Assert.Contains("test_throwing", toolMsg.Content);

        // LLM should still continue and produce final text
        Assert.Contains("I see the tool failed", output);
    }

    // ── Tests: events fired during loop ───────────────────────────

    [Fact]
    public async Task E2E_OnToolCallAndOnToolResult_EventsFired()
    {
        var echo = new FakeTool("test_echo", "result");
        var chat = new ScriptedChatService(
            [ToolCallChunk(0, "c1", "test_echo", """{}"""), ToolCallEnd(0)],
            [TextChunk("done")]
        );

        var session = CreateSession(chat, [echo]);
        var toolCalls = new List<(string Name, string Args)>();
        var toolResults = new List<(string Result, string Name)>();
        session.OnToolCall += (name, args) => toolCalls.Add((name, args));
        session.OnToolResult += (result, name, _) => toolResults.Add((result, name));

        await foreach (var _ in session.SendMessageAsync("test")) { }

        Assert.Single(toolCalls);
        Assert.Equal("test_echo", toolCalls[0].Name);
        Assert.Single(toolResults);
        Assert.Equal("result", toolResults[0].Result);
        Assert.Equal("test_echo", toolResults[0].Name);
    }

    [Fact]
    public async Task E2E_OnTextGenerated_FiresForEachDelta()
    {
        var chat = new ScriptedChatService(
            [TextChunk("Hello "), TextChunk("World")]
        );

        var session = CreateSession(chat);
        var deltas = new List<string>();
        session.OnTextGenerated += delta => deltas.Add(delta);

        await foreach (var _ in session.SendMessageAsync("hi")) { }

        Assert.Equal(2, deltas.Count);
        Assert.Equal("Hello ", deltas[0]);
        Assert.Equal("World", deltas[1]);
    }

    // ── Tests: request passed to LLM includes tool results ─────────

    [Fact]
    public async Task E2E_SecondIterationRequest_IncludesToolResult()
    {
        var echo = new FakeTool("test_echo", "feedback-data");
        var chat = new ScriptedChatService(
            [ToolCallChunk(0, "c1", "test_echo", """{}"""), ToolCallEnd(0)],
            [TextChunk("final")]
        );

        var session = CreateSession(chat, [echo]);
        await foreach (var _ in session.SendMessageAsync("test")) { }

        // The second StreamAsync call (iteration 1) should have the tool
        // result message in its request.
        Assert.Equal(2, chat.ReceivedRequests.Count);
        var secondRequest = chat.ReceivedRequests[1];
        var toolMsg = secondRequest.Messages.FirstOrDefault(m => m.Role == "tool");
        Assert.NotNull(toolMsg);
        Assert.Contains("feedback-data", toolMsg!.Content);
    }

    // ── Tests: skills integration ─────────────────────────────────

    [Fact]
    public async Task E2E_SlashCommand_ActivatesSkill_AndStripsCommand()
    {
        // A skill with a slash command. When the user sends "/test-skill
        // my request", the skill should activate, the slash command should
        // be stripped from the user message, and the augmentation should
        // be injected.
        var skill = new TestSkill(
            name: "test-skill",
            slashCommand: "/test-skill",
            displayName: "Test Skill",
            description: "test",
            augment: "SKILL_AUGMENT_TEXT",
            shouldActivate: (_, _) => false); // only via slash command

        var registry = new SkillRegistry();
        registry.Register(skill);

        var chat = new ScriptedChatService([TextChunk("response")]);
        var session = CreateSession(chat, skills: registry);

        await foreach (var _ in session.SendMessageAsync("/test-skill do something")) { }

        // The user message in history should have the slash command stripped
        var userMsg = session.History.First(h => h.Role == "user");
        Assert.DoesNotContain("/test-skill", userMsg.Content);
        Assert.Contains("do something", userMsg.Content);

        // The skill augmentation should be injected as a system message
        Assert.Contains(session.History, h =>
            h.Role == "system" && h.Content.Contains("SKILL_AUGMENT_TEXT"));
    }

    [Fact]
    public async Task E2E_AutoActivatedSkill_InjectsAugmentation()
    {
        var skill = new TestSkill(
            name: "auto-skill",
            slashCommand: null,
            displayName: "Auto Skill",
            description: "test",
            augment: "AUTO_AUGMENT",
            shouldActivate: (msg, _) => msg.Contains("create item"));

        var registry = new SkillRegistry();
        registry.Register(skill);

        var chat = new ScriptedChatService([TextChunk("ok")]);
        var session = CreateSession(chat, skills: registry);

        await foreach (var _ in session.SendMessageAsync("please create item for me")) { }

        Assert.Contains(session.History, h =>
            h.Role == "system" && h.Content.Contains("AUTO_AUGMENT"));
    }

    [Fact]
    public async Task E2E_SkillPreFetch_AddsContextMessages()
    {
        var skill = new TestSkill(
            name: "prefetch-skill",
            slashCommand: "/prefetch",
            displayName: "Prefetch",
            description: "test",
            augment: null,
            shouldActivate: (_, _) => false,
            preFetchMessages: [ChatMessage.System("PRE_FETCHED_CONTEXT")]);

        var registry = new SkillRegistry();
        registry.Register(skill);

        var chat = new ScriptedChatService([TextChunk("done")]);
        var session = CreateSession(chat, skills: registry);

        await foreach (var _ in session.SendMessageAsync("/prefetch build it")) { }

        // Pre-fetched context should appear in history before the user message
        Assert.Contains(session.History, h =>
            h.Role == "system" && h.Content == "PRE_FETCHED_CONTEXT");
    }

    // ── Test skill implementation ─────────────────────────────────

    private sealed class TestSkill : ISkill
    {
        private readonly Func<string, string?, bool> _shouldActivate;
        private readonly IReadOnlyList<ChatMessage> _preFetchMessages;

        public string Name { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string? SlashCommand { get; }
        public string? Augment { get; }

        public TestSkill(
            string name,
            string? slashCommand,
            string displayName,
            string description,
            string? augment,
            Func<string, string?, bool> shouldActivate,
            IReadOnlyList<ChatMessage>? preFetchMessages = null)
        {
            Name = name;
            SlashCommand = slashCommand;
            DisplayName = displayName;
            Description = description;
            Augment = augment;
            _shouldActivate = shouldActivate;
            _preFetchMessages = preFetchMessages ?? [];
        }

        public bool ShouldActivate(string userMessage, string? projectDir)
            => _shouldActivate(userMessage, projectDir);

        public string? GetPromptAugmentation() => Augment;

        public Task<IReadOnlyList<ChatMessage>> PreFetchContextAsync(
            string userMessage, IRetriever? retriever, CancellationToken ct = default)
            => Task.FromResult(_preFetchMessages);
    }
}
