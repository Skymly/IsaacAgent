using IsaacAgent.App.Services;
using IsaacAgent.Core.Models;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Chat session store seam (#47): project manifest round-trip via injectable root.
/// </summary>
public class ChatSessionStoreTests
{
    [Fact]
    public async Task SaveThenLoad_RestoresStableGuidsTitlesAndAgentEnvelopes()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new FileChatSessionStore(root);
            var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_proj_{Guid.NewGuid():N}");
            var tabA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            var tabB = Guid.Parse("11111111-2222-3333-4444-555555555555");

            var assistantWithTools = ChatMessage.Assistant("calling tools");
            assistantWithTools.ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_1",
                    Name = "search_isaac_api",
                    Arguments = """{"query":"tear"}"""
                }
            ];

            var manifest = new ProjectSessionManifest
            {
                ProjectDir = projectDir,
                SavedAt = DateTimeOffset.Parse("2026-07-27T12:00:00Z"),
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = tabA,
                        Title = "Item ideas",
                        HistoryVersion = 1,
                        Messages =
                        [
                            ChatMessage.System("system prompt"),
                            ChatMessage.User("add a tear item"),
                            assistantWithTools,
                            ChatMessage.Tool("call_1", "found TearRate"),
                            ChatMessage.Assistant("Done.")
                        ]
                    },
                    new SessionTabRecord
                    {
                        Id = tabB,
                        Title = "Boss AI",
                        HistoryVersion = 1,
                        Messages =
                        [
                            ChatMessage.User("boss pattern"),
                            ChatMessage.Assistant("use MC_POST_NPC_UPDATE")
                        ]
                    }
                ]
            };

            await store.SaveAsync(projectDir, manifest);

            var loaded = await store.LoadAsync(projectDir);

            Assert.Equal(2, loaded.Tabs.Count);
            Assert.Equal(tabA, loaded.Tabs[0].Id);
            Assert.Equal("Item ideas", loaded.Tabs[0].Title);
            Assert.Equal(1, loaded.Tabs[0].HistoryVersion);
            Assert.Equal(5, loaded.Tabs[0].Messages.Count);
            Assert.Equal("system", loaded.Tabs[0].Messages[0].Role);
            Assert.Equal("user", loaded.Tabs[0].Messages[1].Role);
            Assert.Equal("add a tear item", loaded.Tabs[0].Messages[1].Content);
            Assert.Equal("assistant", loaded.Tabs[0].Messages[2].Role);
            Assert.Single(loaded.Tabs[0].Messages[2].ToolCalls);
            Assert.Equal("search_isaac_api", loaded.Tabs[0].Messages[2].ToolCalls[0].Name);
            Assert.Equal("tool", loaded.Tabs[0].Messages[3].Role);
            Assert.Equal("call_1", loaded.Tabs[0].Messages[3].ToolCallId);
            Assert.Equal("Done.", loaded.Tabs[0].Messages[4].Content);

            Assert.Equal(tabB, loaded.Tabs[1].Id);
            Assert.Equal("Boss AI", loaded.Tabs[1].Title);
            Assert.Equal(2, loaded.Tabs[1].Messages.Count);
            Assert.Equal("boss pattern", loaded.Tabs[1].Messages[0].Content);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task SaveAndLoad_WithNoProject_AreNoOps()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new FileChatSessionStore(root);
            var manifest = new ProjectSessionManifest
            {
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = Guid.NewGuid(),
                        Title = "orphan",
                        Messages = [ChatMessage.User("should not persist")]
                    }
                ]
            };

            await store.SaveAsync(null, manifest);
            await store.SaveAsync("", manifest);
            await store.SaveAsync("   ", manifest);

            Assert.Empty(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories));

            var loadedNull = await store.LoadAsync(null);
            var loadedEmpty = await store.LoadAsync("");
            Assert.Empty(loadedNull.Tabs);
            Assert.Empty(loadedEmpty.Tabs);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Load_CorruptOrUnreadableFile_FailsSoftWithEmptySession()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new FileChatSessionStore(root);
            var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_proj_{Guid.NewGuid():N}");
            var path = store.GetStorePath(projectDir)!;
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(path, "{ not valid json");

            var loaded = await store.LoadAsync(projectDir);

            Assert.Empty(loaded.Tabs);
            Assert.Equal("", loaded.ProjectDir);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsEmptySession()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new FileChatSessionStore(root);
            var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_proj_{Guid.NewGuid():N}");

            var loaded = await store.LoadAsync(projectDir);

            Assert.Empty(loaded.Tabs);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Save_PersistedJson_ExcludesCheckpointsBeforeImagesAndTipHashes()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new FileChatSessionStore(root);
            var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_proj_{Guid.NewGuid():N}");
            var manifest = new ProjectSessionManifest
            {
                ProjectDir = projectDir,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = Guid.NewGuid(),
                        Title = "tab",
                        Messages =
                        [
                            ChatMessage.User("hi"),
                            ChatMessage.Assistant("hello")
                        ]
                    }
                ]
            };

            await store.SaveAsync(projectDir, manifest);

            var path = store.GetStorePath(projectDir)!;
            var json = await File.ReadAllTextAsync(path);

            Assert.DoesNotContain("Checkpoint", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BeforeImage", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BeforeImages", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TipHash", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TrackedWriteTip", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Messages\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Id\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"Title\"", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Save_UsesStableProjectHashPath()
    {
        var root = CreateTempRoot();
        try
        {
            var store = new FileChatSessionStore(root);
            var projectDir = @"C:\Mods\MyCoolMod";
            var expectedHash = FileChatSessionStore.ComputeProjectHash(projectDir);

            await store.SaveAsync(projectDir, new ProjectSessionManifest
            {
                Tabs = [new SessionTabRecord { Id = Guid.NewGuid(), Title = "t" }]
            });

            var expectedPath = Path.Combine(root, $"{expectedHash}.json");
            Assert.True(File.Exists(expectedPath));
            Assert.Equal(expectedPath, store.GetStorePath(projectDir));
            Assert.Equal(
                FileChatSessionStore.ComputeProjectHash(projectDir.ToUpperInvariant()),
                expectedHash);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"isaac_sessions_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
