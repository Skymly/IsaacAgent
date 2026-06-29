using System.Runtime.CompilerServices;
using IsaacAgent.Agent;
using IsaacAgent.Agent.Engine;
using IsaacAgent.App.ViewModels;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for ChatViewModel — multi-tab management, active tab
///   switching, close behavior, and project change propagation.
/// </summary>
[Collection("Avalonia")]
public class ChatViewModelTests
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

    // ── Initialization ────────────────────────────────────────

    [Fact]
    public void Constructor_CreatesOneTabByDefault()
    {
        var vm = CreateChatViewModel();
        Assert.Single(vm.Tabs);
        Assert.NotNull(vm.ActiveTab);
        Assert.Equal(vm.Tabs[0], vm.ActiveTab);
    }

    [Fact]
    public void Constructor_FirstTab_IsNamedChat1()
    {
        var vm = CreateChatViewModel();
        Assert.Equal("Chat 1", vm.Tabs[0].Title);
    }

    // ── AddTab ────────────────────────────────────────────────

    [Fact]
    public void AddTab_IncrementsTabCountAndSetsActive()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal(vm.Tabs[1], vm.ActiveTab);
        Assert.True(vm.Tabs[1].IsActive);
        Assert.False(vm.Tabs[0].IsActive);
    }

    [Fact]
    public void AddTab_SecondTab_IsNamedChat2()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);
        Assert.Equal("Chat 2", vm.Tabs[1].Title);
    }

    [Fact]
    public void AddTab_CanCloseTabsBecomesTrue()
    {
        var vm = CreateChatViewModel();
        Assert.False(vm.CanCloseTabs);

        vm.AddTabCommand.Execute(null);
        Assert.True(vm.CanCloseTabs);
    }

    // ── CloseTab ──────────────────────────────────────────────

    [Fact]
    public void CloseTab_RemovesTabAndAdjustsActive()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);
        var secondTab = vm.Tabs[1];
        Assert.Equal(secondTab, vm.ActiveTab);

        vm.CloseTabCommand.Execute(secondTab);

        Assert.Single(vm.Tabs);
        Assert.Equal(vm.Tabs[0], vm.ActiveTab);
    }

    [Fact]
    public void CloseTab_LastTabCannotBeClosed()
    {
        var vm = CreateChatViewModel();
        var onlyTab = vm.Tabs[0];

        vm.CloseTabCommand.Execute(onlyTab);

        Assert.Single(vm.Tabs);
        Assert.Same(onlyTab, vm.Tabs[0]);
    }

    [Fact]
    public void CloseTab_NullParameter_DoesNothing()
    {
        var vm = CreateChatViewModel();
        vm.CloseTabCommand.Execute(null);
        Assert.Single(vm.Tabs);
    }

    [Fact]
    public void CloseTab_MiddleTab_AdjustsActiveToRemaining()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);
        vm.AddTabCommand.Execute(null);
        Assert.Equal(3, vm.Tabs.Count);

        // Close the middle tab
        var middle = vm.Tabs[1];
        vm.CloseTabCommand.Execute(middle);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.DoesNotContain(middle, vm.Tabs);
    }

    [Fact]
    public void CloseTab_CanCloseTabsBecomesFalseWhenOneRemains()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);
        Assert.True(vm.CanCloseTabs);

        vm.CloseTabCommand.Execute(vm.Tabs[1]);
        Assert.False(vm.CanCloseTabs);
    }

    // ── SelectTab ─────────────────────────────────────────────

    [Fact]
    public void SelectTab_SetsActiveTab()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);
        vm.AddTabCommand.Execute(null);

        var first = vm.Tabs[0];
        vm.SelectTabCommand.Execute(first);

        Assert.Equal(first, vm.ActiveTab);
        Assert.True(first.IsActive);
        Assert.False(vm.Tabs[1].IsActive);
        Assert.False(vm.Tabs[2].IsActive);
    }

    [Fact]
    public void SelectTab_NullParameter_DoesNothing()
    {
        var vm = CreateChatViewModel();
        var current = vm.ActiveTab;
        vm.SelectTabCommand.Execute(null);
        Assert.Same(current, vm.ActiveTab);
    }

    // ── SwitchToNextTab ───────────────────────────────────────

    [Fact]
    public void SwitchToNextTab_SingleTab_DoesNothing()
    {
        var vm = CreateChatViewModel();
        var current = vm.ActiveTab;
        vm.SwitchToNextTabCommand.Execute(null);
        Assert.Same(current, vm.ActiveTab);
    }

    [Fact]
    public void SwitchToNextTab_MultipleTabs_CyclesForward()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);
        vm.AddTabCommand.Execute(null);
        Assert.Equal(3, vm.Tabs.Count);

        // Start at tab 2 (the last added tab is active)
        Assert.Same(vm.Tabs[2], vm.ActiveTab);

        // Switch to next (wraps to tab 0)
        vm.SwitchToNextTabCommand.Execute(null);
        Assert.Same(vm.Tabs[0], vm.ActiveTab);

        // Switch to next (tab 1)
        vm.SwitchToNextTabCommand.Execute(null);
        Assert.Same(vm.Tabs[1], vm.ActiveTab);

        // Switch to next (tab 2)
        vm.SwitchToNextTabCommand.Execute(null);
        Assert.Same(vm.Tabs[2], vm.ActiveTab);
    }

    // ── OnProjectChanged ──────────────────────────────────────

    [Fact]
    public void OnProjectChanged_PropagatesToAllTabs()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);

        // Add a message to each tab to verify they get cleared
        vm.Tabs[0].Messages.Add(new ChatMessageViewModel { Role = "user", Content = "old1" });
        vm.Tabs[1].Messages.Add(new ChatMessageViewModel { Role = "user", Content = "old2" });

        vm.OnProjectChanged("/test/project");

        Assert.Empty(vm.Tabs[0].Messages);
        Assert.Empty(vm.Tabs[1].Messages);
    }

    [Fact]
    public void OnProjectChanged_NullDir_PropagatesToAllTabs()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);

        vm.Tabs[0].Messages.Add(new ChatMessageViewModel { Role = "user", Content = "msg" });

        vm.OnProjectChanged(null);

        Assert.Empty(vm.Tabs[0].Messages);
    }

    // ── ClearMessages ─────────────────────────────────────────

    [Fact]
    public void ClearMessages_ClearsActiveTabOnly()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);

        vm.Tabs[0].Messages.Add(new ChatMessageViewModel { Role = "user", Content = "tab0" });
        vm.Tabs[1].Messages.Add(new ChatMessageViewModel { Role = "user", Content = "tab1" });

        // Active tab is tabs[1]
        vm.ClearMessages();

        Assert.NotEmpty(vm.Tabs[0].Messages);
        Assert.Empty(vm.Tabs[1].Messages);
    }

    // ── Dispose ───────────────────────────────────────────────

    [Fact]
    public void Dispose_ClearsAllTabs()
    {
        var vm = CreateChatViewModel();
        vm.AddTabCommand.Execute(null);
        Assert.Equal(2, vm.Tabs.Count);

        vm.Dispose();

        Assert.Empty(vm.Tabs);
    }
}
