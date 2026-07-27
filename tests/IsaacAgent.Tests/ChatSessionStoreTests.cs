using System.Text;
using System.Text.Json;
using IsaacAgent.App.Services;
using IsaacAgent.Core.Models;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Chat session store seam (#47 round-trip, #48 legacy migrate-once) via injectable roots.
/// </summary>
public class ChatSessionStoreTests
{
    [Fact]
    public async Task SaveThenLoad_RestoresStableGuidsTitlesAndAgentEnvelopes()
    {
        var roots = CreateTempRoots();
        try
        {
            var store = CreateStore(roots);
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
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task SaveAndLoad_WithNoProject_AreNoOps()
    {
        var roots = CreateTempRoots();
        try
        {
            var store = CreateStore(roots);
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

            Assert.False(await store.SaveAsync(null, manifest));
            Assert.False(await store.SaveAsync("", manifest));
            Assert.False(await store.SaveAsync("   ", manifest));

            Assert.Empty(Directory.GetFiles(roots.Sessions, "*.json", SearchOption.AllDirectories));

            var loadedNull = await store.LoadAsync(null);
            var loadedEmpty = await store.LoadAsync("");
            Assert.Empty(loadedNull.Tabs);
            Assert.Empty(loadedEmpty.Tabs);
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_CorruptOrUnreadableFile_FailsSoftWithEmptySession()
    {
        var roots = CreateTempRoots();
        try
        {
            var store = CreateStore(roots);
            var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_proj_{Guid.NewGuid():N}");
            var path = store.GetStorePath(projectDir)!;
            Directory.CreateDirectory(roots.Sessions);
            await File.WriteAllTextAsync(path, "{ not valid json");

            var loaded = await store.LoadAsync(projectDir);

            Assert.Empty(loaded.Tabs);
            Assert.Equal("", loaded.ProjectDir);
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsEmptySession()
    {
        var roots = CreateTempRoots();
        try
        {
            var store = CreateStore(roots);
            var projectDir = Path.Combine(Path.GetTempPath(), $"isaac_proj_{Guid.NewGuid():N}");

            var loaded = await store.LoadAsync(projectDir);

            Assert.Empty(loaded.Tabs);
            Assert.True(File.Exists(store.GetStorePath(projectDir)!));
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_CleanFirstOpen_WritesEmptyStoreAndIgnoresLaterLegacy()
    {
        var roots = CreateTempRoots();
        try
        {
            var projectDir = @"C:\Mods\CleanFirstOpenMod";
            var store = CreateStore(roots);

            var first = await store.LoadAsync(projectDir);
            Assert.Empty(first.Tabs);
            Assert.True(File.Exists(store.GetStorePath(projectDir)!));

            var hash = FileChatSessionStore.ComputeProjectHash(projectDir);
            await WriteLegacyHistoryAsync(
                roots.History,
                hash,
                "late9999",
                [ChatMessage.User("appeared after clean open")]);

            var second = await store.LoadAsync(projectDir);
            Assert.Empty(second.Tabs);
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Save_PersistedJson_ExcludesCheckpointsBeforeImagesAndTipHashes()
    {
        var roots = CreateTempRoots();
        try
        {
            var store = CreateStore(roots);
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
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Save_UsesStableProjectHashPath()
    {
        var roots = CreateTempRoots();
        try
        {
            var store = CreateStore(roots);
            var projectDir = @"C:\Mods\MyCoolMod";
            var expectedHash = FileChatSessionStore.ComputeProjectHash(projectDir);

            await store.SaveAsync(projectDir, new ProjectSessionManifest
            {
                Tabs = [new SessionTabRecord { Id = Guid.NewGuid(), Title = "t" }]
            });

            var expectedPath = Path.Combine(roots.Sessions, $"{expectedHash}.json");
            Assert.True(File.Exists(expectedPath));
            Assert.Equal(expectedPath, store.GetStorePath(projectDir));
            Assert.Equal(
                FileChatSessionStore.ComputeProjectHash(projectDir.ToUpperInvariant()),
                expectedHash);
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_MigratesOnceFromLegacyHistoryAndChatHistory_PreferringHistoryContentAndChatHistoryTitlesOrder()
    {
        var roots = CreateTempRoots();
        try
        {
            var projectDir = @"C:\Mods\LegacyMigrateMod";
            var hash = FileChatSessionStore.ComputeProjectHash(projectDir);

            await WriteLegacyHistoryAsync(
                roots.History,
                hash,
                "aaaa1111",
                [
                    ChatMessage.System("sys"),
                    ChatMessage.User("from history tab A"),
                    ChatMessage.Assistant("reply A with tools")
                ]);
            await WriteLegacyHistoryAsync(
                roots.History,
                hash,
                "bbbb2222",
                [
                    ChatMessage.User("from history tab B"),
                    ChatMessage.Assistant("reply B")
                ]);

            await WriteLegacyChatHistoryAsync(
                roots.ChatHistory,
                projectDir,
                [
                    ("Item ideas", [new ChatMessageRecord { Role = "user", Content = "ui only A" }]),
                    ("Boss AI", [new ChatMessageRecord { Role = "user", Content = "ui only B" }])
                ]);

            var historyBytesBefore = SnapshotDirectory(roots.History);
            var chatHistoryBytesBefore = SnapshotDirectory(roots.ChatHistory);

            var store = CreateStore(roots);
            var loaded = await store.LoadAsync(projectDir);

            Assert.Equal(2, loaded.Tabs.Count);
            Assert.Equal("Item ideas", loaded.Tabs[0].Title);
            Assert.Equal("Boss AI", loaded.Tabs[1].Title);
            Assert.Equal("from history tab A", loaded.Tabs[0].Messages[1].Content);
            Assert.Equal("from history tab B", loaded.Tabs[1].Messages[0].Content);
            Assert.DoesNotContain(loaded.Tabs[0].Messages, m => m.Content == "ui only A");
            Assert.NotEqual(Guid.Empty, loaded.Tabs[0].Id);
            Assert.NotEqual(loaded.Tabs[0].Id, loaded.Tabs[1].Id);

            var storePath = store.GetStorePath(projectDir)!;
            Assert.True(File.Exists(storePath));

            Assert.Equal(historyBytesBefore, SnapshotDirectory(roots.History));
            Assert.Equal(chatHistoryBytesBefore, SnapshotDirectory(roots.ChatHistory));
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_AfterMigration_IgnoresLegacyAuthorityOnSubsequentLoad()
    {
        var roots = CreateTempRoots();
        try
        {
            var projectDir = @"C:\Mods\MigrateOnceMod";
            var hash = FileChatSessionStore.ComputeProjectHash(projectDir);

            await WriteLegacyHistoryAsync(
                roots.History,
                hash,
                "cccc3333",
                [ChatMessage.User("original migrated content")]);
            await WriteLegacyChatHistoryAsync(
                roots.ChatHistory,
                projectDir,
                [("Original title", [new ChatMessageRecord { Role = "user", Content = "ui" }])]);

            var store = CreateStore(roots);
            var first = await store.LoadAsync(projectDir);
            Assert.Single(first.Tabs);
            Assert.Equal("original migrated content", first.Tabs[0].Messages[0].Content);
            Assert.Equal("Original title", first.Tabs[0].Title);
            var migratedId = first.Tabs[0].Id;

            await WriteLegacyHistoryAsync(
                roots.History,
                hash,
                "cccc3333",
                [ChatMessage.User("mutated legacy content")]);
            await WriteLegacyChatHistoryAsync(
                roots.ChatHistory,
                projectDir,
                [("Mutated title", [new ChatMessageRecord { Role = "user", Content = "mutated ui" }])]);

            var second = await store.LoadAsync(projectDir);

            Assert.Single(second.Tabs);
            Assert.Equal(migratedId, second.Tabs[0].Id);
            Assert.Equal("Original title", second.Tabs[0].Title);
            Assert.Equal("original migrated content", second.Tabs[0].Messages[0].Content);
            Assert.DoesNotContain(second.Tabs[0].Messages, m => m.Content == "mutated legacy content");
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_WhenSessionsStoreAlreadyExists_DoesNotReadLegacy()
    {
        var roots = CreateTempRoots();
        try
        {
            var projectDir = @"C:\Mods\ExistingStoreMod";
            var hash = FileChatSessionStore.ComputeProjectHash(projectDir);
            var store = CreateStore(roots);
            var existingId = Guid.Parse("dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb");

            await store.SaveAsync(projectDir, new ProjectSessionManifest
            {
                ProjectDir = projectDir,
                Tabs =
                [
                    new SessionTabRecord
                    {
                        Id = existingId,
                        Title = "Already in store",
                        Messages = [ChatMessage.User("store wins")]
                    }
                ]
            });

            await WriteLegacyHistoryAsync(
                roots.History,
                hash,
                "eeee4444",
                [ChatMessage.User("legacy should be ignored")]);
            await WriteLegacyChatHistoryAsync(
                roots.ChatHistory,
                projectDir,
                [("Legacy title", [new ChatMessageRecord { Role = "user", Content = "legacy ui" }])]);

            var loaded = await store.LoadAsync(projectDir);

            Assert.Single(loaded.Tabs);
            Assert.Equal(existingId, loaded.Tabs[0].Id);
            Assert.Equal("Already in store", loaded.Tabs[0].Title);
            Assert.Equal("store wins", loaded.Tabs[0].Messages[0].Content);
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_HistoryOnly_MigratesEnvelopesWithDefaultTitles()
    {
        var roots = CreateTempRoots();
        try
        {
            var projectDir = @"C:\Mods\HistoryOnlyMod";
            var hash = FileChatSessionStore.ComputeProjectHash(projectDir);
            await WriteLegacyHistoryAsync(
                roots.History,
                hash,
                "ffff5555",
                [ChatMessage.User("solo history")]);

            var store = CreateStore(roots);
            var loaded = await store.LoadAsync(projectDir);

            Assert.Single(loaded.Tabs);
            Assert.Equal("solo history", loaded.Tabs[0].Messages[0].Content);
            Assert.False(string.IsNullOrWhiteSpace(loaded.Tabs[0].Title));
            Assert.True(File.Exists(store.GetStorePath(projectDir)!));
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_ChatHistoryOnly_MigratesTitlesOrderAndConvertedMessages()
    {
        var roots = CreateTempRoots();
        try
        {
            var projectDir = @"C:\Mods\ChatHistoryOnlyMod";
            await WriteLegacyChatHistoryAsync(
                roots.ChatHistory,
                projectDir,
                [
                    ("First", [new ChatMessageRecord { Role = "user", Content = "u1" }, new ChatMessageRecord { Role = "assistant", Content = "a1" }]),
                    ("Second", [new ChatMessageRecord { Role = "user", Content = "u2" }])
                ]);

            var store = CreateStore(roots);
            var loaded = await store.LoadAsync(projectDir);

            Assert.Equal(2, loaded.Tabs.Count);
            Assert.Equal("First", loaded.Tabs[0].Title);
            Assert.Equal("Second", loaded.Tabs[1].Title);
            Assert.Equal(2, loaded.Tabs[0].Messages.Count);
            Assert.Equal("u1", loaded.Tabs[0].Messages[0].Content);
            Assert.Equal("a1", loaded.Tabs[0].Messages[1].Content);
            Assert.Equal("u2", loaded.Tabs[1].Messages[0].Content);
        }
        finally
        {
            Cleanup(roots);
        }
    }

    [Fact]
    public async Task Load_ConcurrentFirstOpen_SharesSingleMigratedAuthority()
    {
        var roots = CreateTempRoots();
        try
        {
            var projectDir = @"C:\Mods\ConcurrentMigrateMod";
            var hash = FileChatSessionStore.ComputeProjectHash(projectDir);
            await WriteLegacyHistoryAsync(
                roots.History,
                hash,
                "abcd1234",
                [ChatMessage.User("shared content")]);
            await WriteLegacyChatHistoryAsync(
                roots.ChatHistory,
                projectDir,
                [("Shared", [new ChatMessageRecord { Role = "user", Content = "ui" }])]);

            var store = CreateStore(roots);
            var tasks = Enumerable.Range(0, 8)
                .Select(_ => store.LoadAsync(projectDir))
                .ToArray();
            var results = await Task.WhenAll(tasks);

            Assert.All(results, r =>
            {
                Assert.Single(r.Tabs);
                Assert.Equal("Shared", r.Tabs[0].Title);
                Assert.Equal("shared content", r.Tabs[0].Messages[0].Content);
            });

            var ids = results.Select(r => r.Tabs[0].Id).Distinct().ToArray();
            Assert.Single(ids);

            var storePath = store.GetStorePath(projectDir)!;
            Assert.True(File.Exists(storePath));
            Assert.Single(Directory.GetFiles(roots.Sessions, "*.json"));
        }
        finally
        {
            Cleanup(roots);
        }
    }

    private static FileChatSessionStore CreateStore(TempRoots roots) =>
        new(roots.Sessions, roots.History, roots.ChatHistory);

    private static TempRoots CreateTempRoots()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"isaac_sessions_test_{Guid.NewGuid():N}");
        var roots = new TempRoots(
            Path.Combine(baseDir, "sessions"),
            Path.Combine(baseDir, "history"),
            Path.Combine(baseDir, "chat-history"));
        Directory.CreateDirectory(roots.Sessions);
        Directory.CreateDirectory(roots.History);
        Directory.CreateDirectory(roots.ChatHistory);
        return roots;
    }

    private static void Cleanup(TempRoots roots)
    {
        TryDeleteDirectory(Path.GetDirectoryName(roots.Sessions)!);
    }

    private static async Task WriteLegacyHistoryAsync(
        string historyRoot,
        string projectHash,
        string tabId,
        List<ChatMessage> messages)
    {
        var path = Path.Combine(historyRoot, $"project_{projectHash}_{tabId}.json");
        var json = JsonSerializer.Serialize(
            new { Version = 1, Messages = messages },
            new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static async Task WriteLegacyChatHistoryAsync(
        string chatHistoryRoot,
        string projectDir,
        IReadOnlyList<(string Title, List<ChatMessageRecord> Messages)> tabs)
    {
        var safeName = SanitizeFileName(projectDir);
        var path = Path.Combine(chatHistoryRoot, $"{safeName}.json");
        var session = new ChatSessionRecord
        {
            ProjectDir = projectDir,
            SavedAt = DateTimeOffset.UtcNow,
            Tabs = tabs.Select(t => new ChatTabRecord
            {
                Title = t.Title,
                Messages = t.Messages
            }).ToList()
        };
        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static string SanitizeFileName(string path)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new StringBuilder(path.Length);
        foreach (var c in path)
            result.Append(invalid.Contains(c) ? '_' : c);
        return result.ToString()
            .Replace(':', '_')
            .Replace('\\', '_')
            .Replace('/', '_');
    }

    private static Dictionary<string, byte[]> SnapshotDirectory(string directory)
    {
        var snapshot = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(directory))
            return snapshot;

        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file);
            snapshot[relative] = File.ReadAllBytes(file);
        }

        return snapshot;
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

    private sealed record TempRoots(string Sessions, string History, string ChatHistory);
}
