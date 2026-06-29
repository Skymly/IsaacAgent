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
///   Unit tests for MainViewModel — initialization, command delegation,
///   and project loaded event handling.
/// </summary>
[Collection("Avalonia")]
public class MainViewModelTests
{
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
            yield return new ChatChunk("stub", false, -1, null, null, null);
            await Task.CompletedTask;
        }
    }

    private static (MainViewModel vm, IServiceProvider sp) CreateMainViewModel()
    {
        var chat = new StubChatService();
        var session = CreateSession(chat);
        var factoryMock = new Mock<IAgentSessionFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string?>())).Returns(session);

        var services = new ServiceCollection();
        services.AddSingleton(factoryMock.Object);
        services.AddSingleton(Mock.Of<ILogger<ChatTabViewModel>>());
        services.AddSingleton(Mock.Of<ILogger<ChatViewModel>>());
        services.AddSingleton(Mock.Of<ILogger<MainViewModel>>());
        services.AddSingleton(Mock.Of<ILogger<ProjectViewModel>>());
        services.AddSingleton(new AppConfiguration());
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<ProjectViewModel>();
        services.AddSingleton<QuickReferenceViewModel>();
        services.AddSingleton<LogMonitorService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<MainViewModel>();
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<MainViewModel>(), sp);
    }

    private static AgentSession CreateSession(IChatService chat)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(toolLogger);
        return new AgentSession(chat, registry, null, logger, null);
    }

    [Fact]
    public void Constructor_InitializesAllProperties()
    {
        var (vm, _) = CreateMainViewModel();
        Assert.NotNull(vm.Chat);
        Assert.NotNull(vm.Project);
        Assert.NotNull(vm.QuickReference);
        Assert.NotNull(vm.LogMonitor);
        Assert.NotNull(vm.Toasts);
        Assert.Equal("Ready", vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void ClearChat_SetsStatusText()
    {
        var (vm, _) = CreateMainViewModel();
        vm.ClearChatCommand.Execute(null);
        Assert.Equal("Chat cleared", vm.StatusText);
    }

    [Fact]
    public void ClearChat_ClearsActiveTabMessages()
    {
        var (vm, _) = CreateMainViewModel();
        vm.Chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "test" });
        Assert.NotEmpty(vm.Chat.ActiveTab.Messages);

        vm.ClearChatCommand.Execute(null);

        Assert.Empty(vm.Chat.ActiveTab.Messages);
    }

    [Fact]
    public void StatusText_SetAndGet_WorksCorrectly()
    {
        var (vm, _) = CreateMainViewModel();
        vm.StatusText = "Custom status";
        Assert.Equal("Custom status", vm.StatusText);
    }

    [Fact]
    public void IsBusy_SetAndGet_WorksCorrectly()
    {
        var (vm, _) = CreateMainViewModel();
        vm.IsBusy = true;
        Assert.True(vm.IsBusy);
        vm.IsBusy = false;
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public void Chat_Property_IsSameInstance()
    {
        var (vm, sp) = CreateMainViewModel();
        var chatFromSp = sp.GetRequiredService<ChatViewModel>();
        Assert.Same(chatFromSp, vm.Chat);
    }

    [Fact]
    public void Project_Property_IsSameInstance()
    {
        var (vm, sp) = CreateMainViewModel();
        var projectFromSp = sp.GetRequiredService<ProjectViewModel>();
        Assert.Same(projectFromSp, vm.Project);
    }

    [Fact]
    public void Toasts_Property_IsSameInstance()
    {
        var (vm, sp) = CreateMainViewModel();
        var toastsFromSp = sp.GetRequiredService<ToastService>();
        Assert.Same(toastsFromSp, vm.Toasts);
    }
}
