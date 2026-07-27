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
///   Unit tests for MainViewModel — initialization, command delegation,
///   and Chat session store project-switch hydration (#49).
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

    /// <summary>In-memory Chat session store for project-switch wiring tests.</summary>
    private sealed class MemoryChatSessionStore : IChatSessionStore
    {
        private readonly Dictionary<string, ProjectSessionManifest> _byProject =
            new(StringComparer.OrdinalIgnoreCase);

        public List<(string Op, string? ProjectDir)> Calls { get; } = [];

        public void Seed(string projectDir, ProjectSessionManifest manifest) =>
            _byProject[projectDir] = Clone(manifest);

        public Task<bool> SaveAsync(string? projectDir, ProjectSessionManifest manifest, CancellationToken ct = default)
        {
            Calls.Add(("save", projectDir));
            if (string.IsNullOrWhiteSpace(projectDir))
                return Task.FromResult(false);
            _byProject[projectDir] = Clone(manifest);
            return Task.FromResult(true);
        }

        public Task<ProjectSessionManifest> LoadAsync(string? projectDir, CancellationToken ct = default)
        {
            Calls.Add(("load", projectDir));
            if (string.IsNullOrWhiteSpace(projectDir))
                return Task.FromResult(new ProjectSessionManifest());
            if (_byProject.TryGetValue(projectDir, out var manifest))
                return Task.FromResult(Clone(manifest));
            return Task.FromResult(new ProjectSessionManifest { ProjectDir = projectDir });
        }

        private static ProjectSessionManifest Clone(ProjectSessionManifest source) => new()
        {
            Version = source.Version,
            ProjectDir = source.ProjectDir,
            SavedAt = source.SavedAt,
            Tabs = source.Tabs.Select(t => new SessionTabRecord
            {
                Id = t.Id,
                Title = t.Title,
                HistoryVersion = t.HistoryVersion,
                Messages = t.Messages.ToList()
            }).ToList()
        };
    }

    private static (MainViewModel vm, IServiceProvider sp, MemoryChatSessionStore store)
        CreateMainViewModel(MemoryChatSessionStore? store = null)
    {
        store ??= new MemoryChatSessionStore();
        var chat = new StubChatService();
        var factoryMock = new Mock<IAgentSessionFactory>();
        factoryMock
            .Setup(f => f.Create(It.IsAny<string?>()))
            .Returns((string? dir) => CreateSession(chat, dir));

        var services = new ServiceCollection();
        services.AddSingleton(factoryMock.Object);
        services.AddSingleton(Mock.Of<ILogger<ChatTabViewModel>>());
        services.AddSingleton(Mock.Of<ILogger<ChatViewModel>>());
        services.AddSingleton(Mock.Of<ILogger<MainViewModel>>());
        services.AddSingleton(Mock.Of<ILogger<ProjectViewModel>>());
        services.AddSingleton(Mock.Of<IRestoreConfirmDialog>());
        services.AddSingleton(new AppConfiguration());
        services.AddSingleton<IChatSessionStore>(store);
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<ProjectViewModel>();
        services.AddSingleton<QuickReferenceViewModel>();
        services.AddSingleton<LogMonitorService>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<ChatHistoryService>();
        services.AddSingleton<MainViewModel>();
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<MainViewModel>(), sp, store);
    }

    private static AgentSession CreateSession(IChatService chat, string? projectDir = null)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        var registry = new ToolRegistry(toolLogger);
        return new AgentSession(chat, registry, projectDir, logger, null);
    }

    private static string CreateTempProjectDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"isaac_switch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [AvaloniaFact]
    public void Constructor_InitializesAllProperties()
    {
        var (vm, _, _) = CreateMainViewModel();
        Assert.NotNull(vm.Chat);
        Assert.NotNull(vm.Project);
        Assert.NotNull(vm.QuickReference);
        Assert.NotNull(vm.LogMonitor);
        Assert.NotNull(vm.Toasts);
        // StatusText is loaded from i18n resources; in headless tests it
        // falls back to the resource key "StatusReady" if resources are
        // not loaded. Just verify it's not null.
        Assert.NotNull(vm.StatusText);
        Assert.False(vm.IsBusy);
    }

    [AvaloniaFact]
    public void ClearChat_SetsStatusText()
    {
        var (vm, _, _) = CreateMainViewModel();
        vm.ClearChatCommand.Execute(null);
        // StatusText comes from i18n resource key "StatusChatCleared"
        Assert.NotEqual("", vm.StatusText);
    }

    [AvaloniaFact]
    public void ClearChat_ClearsActiveTabMessages()
    {
        var (vm, _, _) = CreateMainViewModel();
        vm.Chat.ActiveTab!.Messages.Add(new ChatMessageViewModel { Role = "user", Content = "test" });
        Assert.NotEmpty(vm.Chat.ActiveTab.Messages);

        vm.ClearChatCommand.Execute(null);

        Assert.Empty(vm.Chat.ActiveTab.Messages);
    }

    [AvaloniaFact]
    public void StatusText_SetAndGet_WorksCorrectly()
    {
        var (vm, _, _) = CreateMainViewModel();
        vm.StatusText = "Custom status";
        Assert.Equal("Custom status", vm.StatusText);
    }

    [AvaloniaFact]
    public void IsBusy_SetAndGet_WorksCorrectly()
    {
        var (vm, _, _) = CreateMainViewModel();
        vm.IsBusy = true;
        Assert.True(vm.IsBusy);
        vm.IsBusy = false;
        Assert.False(vm.IsBusy);
    }

    [AvaloniaFact]
    public void Chat_Property_IsSameInstance()
    {
        var (vm, sp, _) = CreateMainViewModel();
        var chatFromSp = sp.GetRequiredService<ChatViewModel>();
        Assert.Same(chatFromSp, vm.Chat);
    }

    [AvaloniaFact]
    public void Project_Property_IsSameInstance()
    {
        var (vm, sp, _) = CreateMainViewModel();
        var projectFromSp = sp.GetRequiredService<ProjectViewModel>();
        Assert.Same(projectFromSp, vm.Project);
    }

    [AvaloniaFact]
    public void Toasts_Property_IsSameInstance()
    {
        var (vm, sp, _) = CreateMainViewModel();
        var toastsFromSp = sp.GetRequiredService<ToastService>();
        Assert.Same(toastsFromSp, vm.Toasts);
    }

    [AvaloniaFact]
    public async Task ProjectSwitch_StoreHadMessages_HydratesAgentSessionHistory()
    {
        var projectDir = CreateTempProjectDir();
        try
        {
            var tabId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var assistant = ChatMessage.Assistant("calling tools");
            assistant.ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_1",
                    Name = "search_isaac_api",
                    Arguments = """{"query":"tear"}"""
                }
            ];

            var store = new MemoryChatSessionStore();
            store.Seed(projectDir, new ProjectSessionManifest
            {
                ProjectDir = projectDir,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = tabId,
                        Title = "Item ideas",
                        HistoryVersion = 1,
                        Messages =
                        [
                            ChatMessage.System("system prompt"),
                            ChatMessage.User("add a tear item"),
                            assistant,
                            ChatMessage.Tool("call_1", "found TearRate"),
                            ChatMessage.Assistant("Done.")
                        ]
                    }
                ]
            });

            var (vm, _, _) = CreateMainViewModel(store);
            await vm.Project.LoadProjectAsync(projectDir);

            var tab = Assert.Single(vm.Chat.Tabs);
            Assert.Equal(tabId, tab.Id);
            Assert.Equal("Item ideas", tab.Title);
            Assert.True(tab.AgentHistory.Count > 1, "AgentSession history must be non-empty after store hydrate");
            Assert.Contains(tab.AgentHistory, m => m.Role == "user" && m.Content == "add a tear item");
            Assert.Contains(tab.AgentHistory, m => m.Role == "tool" && m.ToolCallId == "call_1");
            Assert.Contains(tab.AgentHistory, m => m.ToolCalls.Count > 0);

            Assert.Equal(3, tab.Messages.Count);
            Assert.All(tab.Messages, m => Assert.True(m.Role is "user" or "assistant"));
            Assert.DoesNotContain(tab.Messages, m => m.Role is "system" or "tool" or "tool_result");
            Assert.Equal("add a tear item", tab.Messages[0].Content);
            Assert.Equal("calling tools", tab.Messages[1].Content);
            Assert.Equal("Done.", tab.Messages[2].Content);
            Assert.Empty(tab.Session.Checkpoints);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [AvaloniaFact]
    public async Task ProjectSwitch_SavesOutgoingBeforeLoadingIncoming()
    {
        var projectA = CreateTempProjectDir();
        var projectB = CreateTempProjectDir();
        try
        {
            var store = new MemoryChatSessionStore();
            var (vm, _, _) = CreateMainViewModel(store);

            await vm.Project.LoadProjectAsync(projectA);
            var tabA = Assert.Single(vm.Chat.Tabs);
            tabA.Title = "Thread A";
            tabA.Session.History.Add(ChatMessage.User("from A"));
            tabA.Session.History.Add(ChatMessage.Assistant("reply A"));

            store.Calls.Clear();
            await vm.Project.LoadProjectAsync(projectB);

            Assert.Equal(2, store.Calls.Count);
            Assert.Equal(("save", projectA), store.Calls[0]);
            Assert.Equal(("load", projectB), store.Calls[1]);

            // Re-open A: messages saved on switch must hydrate AgentSession.
            await vm.Project.LoadProjectAsync(projectA);
            var restored = Assert.Single(vm.Chat.Tabs);
            Assert.Equal("Thread A", restored.Title);
            Assert.Contains(restored.AgentHistory, m => m.Content == "from A");
            Assert.Contains(restored.AgentHistory, m => m.Content == "reply A");
        }
        finally
        {
            if (Directory.Exists(projectA)) Directory.Delete(projectA, true);
            if (Directory.Exists(projectB)) Directory.Delete(projectB, true);
        }
    }

    [AvaloniaFact]
    public async Task ProjectSwitch_RestoresTabOrderTitlesAndStableIds()
    {
        var projectDir = CreateTempProjectDir();
        try
        {
            var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var store = new MemoryChatSessionStore();
            store.Seed(projectDir, new ProjectSessionManifest
            {
                ProjectDir = projectDir,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = id1,
                        Title = "First",
                        Messages = [ChatMessage.User("one"), ChatMessage.Assistant("a1")]
                    },
                    new SessionTabRecord
                    {
                        Id = id2,
                        Title = "Second",
                        Messages = [ChatMessage.User("two"), ChatMessage.Assistant("a2")]
                    }
                ]
            });

            var (vm, _, _) = CreateMainViewModel(store);
            await vm.Project.LoadProjectAsync(projectDir);

            Assert.Equal(2, vm.Chat.Tabs.Count);
            Assert.Equal(id1, vm.Chat.Tabs[0].Id);
            Assert.Equal("First", vm.Chat.Tabs[0].Title);
            Assert.Equal(id2, vm.Chat.Tabs[1].Id);
            Assert.Equal("Second", vm.Chat.Tabs[1].Title);
            Assert.Same(vm.Chat.Tabs[0], vm.Chat.ActiveTab);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [AvaloniaFact]
    public async Task ProjectSwitch_ProjectsAreIsolatedByPath()
    {
        var projectA = CreateTempProjectDir();
        var projectB = CreateTempProjectDir();
        try
        {
            var store = new MemoryChatSessionStore();
            store.Seed(projectA, new ProjectSessionManifest
            {
                ProjectDir = projectA,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = Guid.NewGuid(),
                        Title = "A only",
                        Messages = [ChatMessage.User("secret from A"), ChatMessage.Assistant("ok")]
                    }
                ]
            });
            store.Seed(projectB, new ProjectSessionManifest
            {
                ProjectDir = projectB,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = Guid.NewGuid(),
                        Title = "B only",
                        Messages = [ChatMessage.User("from B"), ChatMessage.Assistant("ok")]
                    }
                ]
            });

            var (vm, _, _) = CreateMainViewModel(store);
            await vm.Project.LoadProjectAsync(projectB);

            var tab = Assert.Single(vm.Chat.Tabs);
            Assert.Equal("B only", tab.Title);
            Assert.Contains(tab.AgentHistory, m => m.Content == "from B");
            Assert.DoesNotContain(tab.AgentHistory, m => m.Content == "secret from A");
            Assert.DoesNotContain(tab.Messages, m => m.Content == "secret from A");
        }
        finally
        {
            if (Directory.Exists(projectA)) Directory.Delete(projectA, true);
            if (Directory.Exists(projectB)) Directory.Delete(projectB, true);
        }
    }

    [AvaloniaFact]
    public async Task ProjectSwitch_DoesNotCallRestoreSession()
    {
        var projectDir = CreateTempProjectDir();
        try
        {
            // Seed only via Chat session store; legacy chat-history is empty.
            var store = new MemoryChatSessionStore();
            store.Seed(projectDir, new ProjectSessionManifest
            {
                ProjectDir = projectDir,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = Guid.NewGuid(),
                        Title = "Store tab",
                        Messages = [ChatMessage.User("via store"), ChatMessage.Assistant("ok")]
                    }
                ]
            });

            var (vm, _, _) = CreateMainViewModel(store);
            await vm.Project.LoadProjectAsync(projectDir);

            Assert.Equal("Store tab", Assert.Single(vm.Chat.Tabs).Title);
            Assert.Contains(vm.Chat.Tabs[0].AgentHistory, m => m.Content == "via store");
            Assert.True(vm.Chat.Tabs[0].AgentHistory.Count > 1);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }
}
