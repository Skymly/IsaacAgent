using IsaacAgent.Agent.Engine;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// AgentSession-seam tests for Checkpoint Restore (issue #38).
/// </summary>
public class AgentSessionRestoreTests
{
    private static AgentSession CreateSession(
        IChatService chat,
        string projectDir,
        ILogger<AgentSession>? logger = null,
        ILogger<ToolRegistry>? toolLogger = null)
    {
        logger ??= Mock.Of<ILogger<AgentSession>>();
        toolLogger ??= Mock.Of<ILogger<ToolRegistry>>();
        return new AgentSession(chat, new ToolRegistry(toolLogger), projectDir, logger);
    }

    private sealed class StubChatService : IChatService
    {
        private readonly ChatChunk[] _chunks;
        public StubChatService(params ChatChunk[] chunks) => _chunks = chunks;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse { Message = new ChatMessage { Role = "assistant", Content = "ok" } });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(
            ChatRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.Yield();
            foreach (var c in _chunks)
                yield return c;
        }
    }

    private static StubChatService ToolThenDone(string toolName, string args, string callId = "call_1") =>
        new(
            new ChatChunk("", true, 0, callId, toolName, args),
            new ChatChunk("", true, 0, null, null, null),
            new ChatChunk("Done", false, -1, null, null, null));

    private static StubChatService TextOnly(string text = "ok") =>
        new(new ChatChunk(text, false, -1, null, null, null));

    private static string CreateTempProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"isaac_restore_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task DrainAsync(IAsyncEnumerable<string> stream)
    {
        await foreach (var _ in stream) { }
    }

    [Fact]
    public async Task Restore_TruncatesCheckpointUserTurnAndLaterConversation()
    {
        var projectDir = CreateTempProject();
        try
        {
            var chat = TextOnly();
            var session = CreateSession(chat, projectDir);

            await DrainAsync(session.SendMessageAsync("keep me"));
            await DrainAsync(session.SendMessageAsync("restore here"));
            await DrainAsync(session.SendMessageAsync("drop me"));

            Assert.Equal(3, session.Checkpoints.Count);
            var target = session.Checkpoints[1];

            var result = await session.RestoreAsync(target.Id);

            Assert.Equal(target.Id, result.CheckpointId);
            Assert.Equal("restore here", result.UserPrompt);
            Assert.DoesNotContain(session.History, m => m.Role == "user" && m.Content == "restore here");
            Assert.DoesNotContain(session.History, m => m.Role == "user" && m.Content == "drop me");
            Assert.Contains(session.History, m => m.Role == "user" && m.Content == "keep me");
            Assert.Single(session.Checkpoints);
            Assert.Equal("keep me", session.Checkpoints[0].UserMessage.Content);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_RevertsTrackedWriteViaBeforeImage()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- original");

            var chat = ToolThenDone("write_file", """{"path":"main.lua","content":"-- mutated"}""");
            var session = CreateSession(chat, projectDir);
            await DrainAsync(session.SendMessageAsync("mutate"));

            Assert.Equal("-- mutated", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));
            var checkpoint = Assert.Single(session.Checkpoints);

            var result = await session.RestoreAsync(checkpoint.Id);

            Assert.Equal("-- original", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));
            Assert.Contains("main.lua", result.RestoredPaths);
            Assert.Empty(result.SkippedPaths);
            Assert.Empty(session.Checkpoints);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_CreateTombstone_DeletesCreatedFile()
    {
        var projectDir = CreateTempProject();
        try
        {
            var chat = ToolThenDone("write_file", """{"path":"new.lua","content":"-- created"}""");
            var session = CreateSession(chat, projectDir);
            await DrainAsync(session.SendMessageAsync("create"));

            Assert.True(File.Exists(Path.Combine(projectDir, "new.lua")));
            var checkpoint = Assert.Single(session.Checkpoints);

            var result = await session.RestoreAsync(checkpoint.Id);

            Assert.False(File.Exists(Path.Combine(projectDir, "new.lua")));
            Assert.Contains("new.lua", result.RestoredPaths);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_MissingBeforeImage_SkipsPath_StillTruncates()
    {
        var projectDir = CreateTempProject();
        try
        {
            var oversized = new string('x', (256 * 1024) + 1);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "big.lua"), oversized);

            var chat = ToolThenDone("write_file", """{"path":"big.lua","content":"small"}""");
            var session = CreateSession(chat, projectDir);
            await DrainAsync(session.SendMessageAsync("overwrite big"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.Empty(checkpoint.BeforeImages);

            var result = await session.RestoreAsync(checkpoint.Id);

            Assert.Equal("small", await File.ReadAllTextAsync(Path.Combine(projectDir, "big.lua")));
            Assert.Empty(result.RestoredPaths);
            var skipped = Assert.Single(result.SkippedPaths);
            Assert.Equal("big.lua", skipped.RelativePath);
            Assert.Equal("missing-before-image", skipped.Reason);
            Assert.Empty(session.Checkpoints);
            Assert.DoesNotContain(session.History, m => m.Role == "user");
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_HandEdit_Force_AppliesBeforeImage()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- original");

            var chat = ToolThenDone("write_file", """{"path":"main.lua","content":"-- agent"}""");
            var session = CreateSession(chat, projectDir);
            await DrainAsync(session.SendMessageAsync("write"));

            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- hand edit");
            var checkpoint = Assert.Single(session.Checkpoints);

            var result = await session.RestoreAsync(checkpoint.Id, HandEditConflictMode.Force);

            Assert.Equal("-- original", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));
            Assert.Contains("main.lua", result.RestoredPaths);
            Assert.Empty(result.SkippedPaths);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_HandEdit_Skip_LeavesPathAndListsIt()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- original");

            var chat = ToolThenDone("write_file", """{"path":"main.lua","content":"-- agent"}""");
            var session = CreateSession(chat, projectDir);
            await DrainAsync(session.SendMessageAsync("write"));

            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- hand edit");
            var checkpoint = Assert.Single(session.Checkpoints);

            var result = await session.RestoreAsync(checkpoint.Id, HandEditConflictMode.Skip);

            Assert.Equal("-- hand edit", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));
            Assert.Empty(result.RestoredPaths);
            var skipped = Assert.Single(result.SkippedPaths);
            Assert.Equal("main.lua", skipped.RelativePath);
            Assert.Equal("hand-edit", skipped.Reason);
            Assert.Empty(session.Checkpoints);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_HandEdit_Skip_DeletedFile_ListsAsHandEdit()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- original");

            var chat = ToolThenDone("write_file", """{"path":"main.lua","content":"-- agent"}""");
            var session = CreateSession(chat, projectDir);
            await DrainAsync(session.SendMessageAsync("write"));

            File.Delete(Path.Combine(projectDir, "main.lua"));
            var checkpoint = Assert.Single(session.Checkpoints);

            var result = await session.RestoreAsync(checkpoint.Id, HandEditConflictMode.Skip);

            Assert.False(File.Exists(Path.Combine(projectDir, "main.lua")));
            var skipped = Assert.Single(result.SkippedPaths);
            Assert.Equal("hand-edit", skipped.Reason);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_DoesNotRevertRunCommandSideEffects()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- original");

            var chat = ToolThenDone(
                "run_command",
                """{"command":"echo mutated> main.lua"}""");
            var session = CreateSession(chat, projectDir);
            await DrainAsync(session.SendMessageAsync("shell"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.Empty(checkpoint.BeforeImages);

            var result = await session.RestoreAsync(checkpoint.Id);

            var disk = await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua"));
            Assert.Contains("mutated", disk, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.RestoredPaths);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_TwoSessions_DoNotShareBeforeImages_LastWriterWins()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- shared original");

            var chatA = ToolThenDone("write_file", """{"path":"main.lua","content":"-- from A"}""");
            var chatB = ToolThenDone("write_file", """{"path":"main.lua","content":"-- from B"}""");
            var sessionA = CreateSession(chatA, projectDir);
            var sessionB = CreateSession(chatB, projectDir);

            await DrainAsync(sessionA.SendMessageAsync("a writes"));
            // B's Before-image captures A's tip ("-- from A")
            await DrainAsync(sessionB.SendMessageAsync("b writes"));

            Assert.Equal("-- shared original", sessionA.Checkpoints[0].BeforeImages["main.lua"].Content);
            Assert.Equal("-- from A", sessionB.Checkpoints[0].BeforeImages["main.lua"].Content);
            Assert.Equal("-- from B", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));

            await sessionB.RestoreAsync(sessionB.Checkpoints[0].Id);
            Assert.Equal("-- from A", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));

            await sessionA.RestoreAsync(sessionA.Checkpoints[0].Id);
            Assert.Equal("-- shared original", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task Restore_LogsStartCompleteAndSkipSummary()
    {
        var projectDir = CreateTempProject();
        try
        {
            var oversized = new string('x', (256 * 1024) + 1);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "big.lua"), oversized);

            var logger = new Mock<ILogger<AgentSession>>();
            var chat = ToolThenDone("write_file", """{"path":"big.lua","content":"small"}""");
            var session = CreateSession(chat, projectDir, logger: logger.Object);
            await DrainAsync(session.SendMessageAsync("overwrite"));

            await session.RestoreAsync(session.Checkpoints[0].Id);

            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("Restore started", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("Restore completed", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("Restore skip summary", StringComparison.Ordinal)
                        && state.ToString()!.Contains("missing-before-image", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }
}
