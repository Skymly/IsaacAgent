using System.Reflection;
using Avalonia.Headless.XUnit;
using System.Runtime.CompilerServices;
using IsaacAgent.Agent;
using IsaacAgent.Agent.Engine;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for ChatHistoryService — live-UI export and search only
///   (Chat session store owns persistence).
/// </summary>
[Collection("Avalonia")]
public class ChatHistoryServiceTests
{
    // ── Test doubles ──────────────────────────────────────────

    private sealed class StubChatService : IChatService
    {
        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse
            {
                Message = new ChatMessage { Role = "assistant", Content = "ok" }
            });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            yield return new ChatChunk("stub", false, -1, null, null, null);
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    private static ChatViewModel CreateChatViewModel()
    {
        var chat = new StubChatService();
        var session = CreateSession(chat);
        var factoryMock = new Mock<IAgentSessionFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string?>())).Returns(session);

        var services = new ServiceCollection();
        services.AddSingleton(factoryMock.Object);
        services.AddSingleton(Mock.Of<ILogger<ChatTabViewModel>>());
        services.AddSingleton(Mock.Of<ILogger<ChatViewModel>>());
        services.AddSingleton(Mock.Of<IRestoreConfirmDialog>());
        services.AddSingleton(Mock.Of<IChatSessionStore>());
        var sp = services.BuildServiceProvider();

        return new ChatViewModel(sp, sp.GetRequiredService<ILogger<ChatViewModel>>());
    }

    private static AgentSession CreateSession(IChatService chat)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(toolLogger);
        return new AgentSession(chat, registry, null, logger, null);
    }

    // ── Dual-path regression ──────────────────────────────────

    [AvaloniaFact]
    public void ChatHistoryService_HasNoDiskPersistenceApi()
    {
        // Guards against reintroducing legacy chat-history/ as an authoritative path.
        string[] forbidden =
        [
            "SaveSession",
            "RestoreSession",
            "LoadSession",
            "DeleteSession",
            "GetHistoryPath"
        ];

        var names = typeof(ChatHistoryService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in forbidden)
            Assert.False(names.Contains(name), $"Legacy dual-path API '{name}' must not exist on ChatHistoryService");
    }

    // ── Export ────────────────────────────────────────────────

    [AvaloniaFact]
    public void ExportToMarkdown_GeneratesValidMarkdown()
    {
        var chat = CreateChatViewModel();
        chat.ActiveTab!.Title = "Test Tab";
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "What is a callback?" });
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "assistant", Content = "A callback is..." });

        var markdown = ChatHistoryService.ExportToMarkdown(chat.ActiveTab);
        Assert.Contains("# Test Tab", markdown);
        Assert.Contains("## User", markdown);
        Assert.Contains("What is a callback?", markdown);
        Assert.Contains("## Assistant", markdown);
        Assert.Contains("A callback is...", markdown);
    }

    [AvaloniaFact]
    public void ExportToJson_GeneratesValidJson()
    {
        var chat = CreateChatViewModel();
        chat.ActiveTab!.Title = "Test Tab";
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Hello" });

        var json = ChatHistoryService.ExportToJson(chat.ActiveTab);
        Assert.Contains("\"Title\": \"Test Tab\"", json);
        Assert.Contains("\"Role\": \"user\"", json);
        Assert.Contains("\"Content\": \"Hello\"", json);
    }

    // ── Search ────────────────────────────────────────────────

    [AvaloniaFact]
    public void SearchMessages_EmptyQuery_ReturnsEmpty()
    {
        var chat = CreateChatViewModel();
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Hello world" });
        var results = ChatHistoryService.SearchMessages(chat, "");
        Assert.Empty(results);
    }

    [AvaloniaFact]
    public void SearchMessages_MatchingQuery_ReturnsResults()
    {
        var chat = CreateChatViewModel();
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Hello world" });
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "assistant", Content = "Goodbye world" });
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Unrelated message" });

        var results = ChatHistoryService.SearchMessages(chat, "world");
        Assert.Equal(2, results.Count);
        Assert.Contains("world", results[0].Message.Content);
        Assert.Contains("world", results[1].Message.Content);
    }

    [AvaloniaFact]
    public void SearchMessages_CaseInsensitive()
    {
        var chat = CreateChatViewModel();
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Hello WORLD" });

        var results = ChatHistoryService.SearchMessages(chat, "world");
        Assert.Single(results);
    }

    [AvaloniaFact]
    public void SearchMessages_AcrossMultipleTabs()
    {
        var chat = CreateChatViewModel();
        chat.AddTabCommand.Execute(null);
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Tab 1 search term" });

        // Switch to second tab and add a message
        var tab2 = chat.Tabs[1];
        tab2.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Tab 2 search term" });

        var results = ChatHistoryService.SearchMessages(chat, "search term");
        Assert.Equal(2, results.Count);
    }
}
