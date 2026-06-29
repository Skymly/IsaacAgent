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
///   Unit tests for ChatHistoryService — persistence, export, and search.
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

    // ── GetHistoryPath ────────────────────────────────────────

    [Fact]
    public void GetHistoryPath_NullProjectDir_ReturnsNull()
    {
        Assert.Null(ChatHistoryService.GetHistoryPath(null));
    }

    [Fact]
    public void GetHistoryPath_EmptyProjectDir_ReturnsNull()
    {
        Assert.Null(ChatHistoryService.GetHistoryPath(""));
    }

    [Fact]
    public void GetHistoryPath_ValidProjectDir_ReturnsPath()
    {
        var path = ChatHistoryService.GetHistoryPath("C:/test/project");
        Assert.NotNull(path);
        Assert.EndsWith(".json", path);
        Assert.Contains("chat-history", path);
    }

    [Fact]
    public void GetHistoryPath_SanitizesInvalidChars()
    {
        // Build a path containing chars that are invalid on the current platform
        var invalidChar = Path.GetInvalidFileNameChars().FirstOrDefault(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar);
        if (invalidChar == '\0') return; // No invalid chars on this platform

        var testPath = $"test{invalidChar}project";
        var path = ChatHistoryService.GetHistoryPath(testPath);
        Assert.NotNull(path);
        var fileName = Path.GetFileName(path!);
        // The invalid char should have been replaced with underscore
        Assert.DoesNotContain(invalidChar.ToString(), fileName);
    }

    // ── Save / Load ───────────────────────────────────────────

    [Fact]
    public void LoadSession_NonExistentProject_ReturnsNull()
    {
        var svc = new ChatHistoryService();
        var result = svc.LoadSession("/nonexistent/path/that/does/not/exist");
        Assert.Null(result);
    }

    [Fact]
    public void SaveAndLoadSession_RoundTrips()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_history_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var svc = new ChatHistoryService();
            var chat = CreateChatViewModel();
            chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Hello" });
            chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "assistant", Content = "Hi there" });

            svc.SaveSession(tempDir, chat);

            var loaded = svc.LoadSession(tempDir);
            Assert.NotNull(loaded);
            Assert.Equal(tempDir, loaded!.ProjectDir);
            Assert.Single(loaded.Tabs);
            Assert.Equal(2, loaded.Tabs[0].Messages.Count);
            Assert.Equal("Hello", loaded.Tabs[0].Messages[0].Content);
            Assert.Equal("Hi there", loaded.Tabs[0].Messages[1].Content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DeleteSession_RemovesHistoryFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_history_del_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var svc = new ChatHistoryService();
            var chat = CreateChatViewModel();
            svc.SaveSession(tempDir, chat);

            var path = ChatHistoryService.GetHistoryPath(tempDir);
            Assert.NotNull(path);
            Assert.True(File.Exists(path));

            svc.DeleteSession(tempDir);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // ── Export ────────────────────────────────────────────────

    [Fact]
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

    [Fact]
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

    [Fact]
    public void SearchMessages_EmptyQuery_ReturnsEmpty()
    {
        var chat = CreateChatViewModel();
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Hello world" });
        var results = ChatHistoryService.SearchMessages(chat, "");
        Assert.Empty(results);
    }

    [Fact]
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

    [Fact]
    public void SearchMessages_CaseInsensitive()
    {
        var chat = CreateChatViewModel();
        chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "Hello WORLD" });

        var results = ChatHistoryService.SearchMessages(chat, "world");
        Assert.Single(results);
    }

    [Fact]
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
