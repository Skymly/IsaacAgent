using IsaacAgent.Agent.Engine;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// AgentSession-seam tests for lazy Before-image capture on Tracked writes (issue #37).
/// </summary>
public class AgentSessionBeforeImageTests
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

    private static string CreateTempProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"isaac_before_image_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task DrainAsync(IAsyncEnumerable<string> stream)
    {
        await foreach (var _ in stream) { }
    }

    [Fact]
    public async Task WriteFile_FirstTrackedWrite_CapturesBeforeImageContent()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- original");

            var chat = ToolThenDone("write_file", """{"path":"main.lua","content":"-- mutated"}""");
            var session = CreateSession(chat, projectDir);

            await DrainAsync(session.SendMessageAsync("edit main"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.True(checkpoint.BeforeImages.TryGetValue("main.lua", out var image));
            Assert.False(image!.IsTombstone);
            Assert.Equal("-- original", image.Content);
            Assert.Equal("-- mutated", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task WriteFile_SecondTouch_DoesNotReplaceFirstBeforeImage()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- original");

            var chat = new StubChatService(
                new ChatChunk("", true, 0, "call_1", "write_file", """{"path":"main.lua","content":"-- first"}"""),
                new ChatChunk("", true, 0, null, null, null),
                new ChatChunk("", true, 1, "call_2", "write_file", """{"path":"main.lua","content":"-- second"}"""),
                new ChatChunk("", true, 1, null, null, null),
                new ChatChunk("Done", false, -1, null, null, null));
            var session = CreateSession(chat, projectDir);

            await DrainAsync(session.SendMessageAsync("edit twice"));

            var checkpoint = Assert.Single(session.Checkpoints);
            var image = Assert.Single(checkpoint.BeforeImages).Value;
            Assert.Equal("-- original", image.Content);
            Assert.Equal("-- second", await File.ReadAllTextAsync(Path.Combine(projectDir, "main.lua")));
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task WriteFile_MissingPath_RecordsCreateTombstone()
    {
        var projectDir = CreateTempProject();
        try
        {
            var chat = ToolThenDone("write_file", """{"path":"new.lua","content":"-- created"}""");
            var session = CreateSession(chat, projectDir);

            await DrainAsync(session.SendMessageAsync("create file"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.True(checkpoint.BeforeImages.TryGetValue("new.lua", out var image));
            Assert.True(image!.IsTombstone);
            Assert.Null(image.Content);
            Assert.True(File.Exists(Path.Combine(projectDir, "new.lua")));
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task BatchEdit_CapturesPerPath_NotAsAtomicSet()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "a.lua"), "aaa");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "b.lua"), "bbb");

            var args = """
                {"edits":[
                  {"path":"a.lua","find":"aaa","replace":"AAA"},
                  {"path":"b.lua","find":"bbb","replace":"BBB"}
                ]}
                """;
            var chat = ToolThenDone("batch_edit", args);
            var session = CreateSession(chat, projectDir);

            await DrainAsync(session.SendMessageAsync("batch"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.Equal(2, checkpoint.BeforeImages.Count);
            Assert.Equal("aaa", checkpoint.BeforeImages["a.lua"].Content);
            Assert.Equal("bbb", checkpoint.BeforeImages["b.lua"].Content);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldMod_CapturesPerPath()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "-- old main");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "metadata.xml"), "<old/>");

            var chat = ToolThenDone(
                "scaffold_mod",
                """{"name":"TestMod","include_items":true}""");
            var session = CreateSession(chat, projectDir);

            await DrainAsync(session.SendMessageAsync("scaffold"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.True(checkpoint.BeforeImages.TryGetValue("main.lua", out var main));
            Assert.Equal("-- old main", main!.Content);
            Assert.True(checkpoint.BeforeImages.TryGetValue("metadata.xml", out var meta));
            Assert.Equal("<old/>", meta!.Content);
            Assert.True(checkpoint.BeforeImages.TryGetValue("items.xml", out var items));
            Assert.True(items!.IsTombstone);
            Assert.False(checkpoint.BeforeImages.ContainsKey("trinkets.xml"));
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task DiffApply_CapturesBeforeImage()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(projectDir, "main.lua"), "line1\nline2\n");

            // Minimal unified diff replacing line2
            var patch = "@@ -1,2 +1,2 @@\n line1\n-line2\n+line2-changed\n";
            var args = JsonSerializer.Serialize(new { path = "main.lua", patch });
            var chat = ToolThenDone("diff_apply", args);
            var session = CreateSession(chat, projectDir);

            await DrainAsync(session.SendMessageAsync("patch"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.True(checkpoint.BeforeImages.TryGetValue("main.lua", out var image));
            Assert.Equal("line1\nline2\n", image!.Content);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task RunCommand_DoesNotCaptureBeforeImage()
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
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task WriteFile_OverLimit_SkipsCapture_AndLogsReason()
    {
        var projectDir = CreateTempProject();
        try
        {
            var oversized = new string('x', (256 * 1024) + 1);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "big.lua"), oversized);

            var logger = new Mock<ILogger<AgentSession>>();
            var chat = ToolThenDone("write_file", """{"path":"big.lua","content":"small"}""");
            var session = CreateSession(chat, projectDir, logger: logger.Object);

            await DrainAsync(session.SendMessageAsync("overwrite big"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.Empty(checkpoint.BeforeImages);
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("Before-image skipped", StringComparison.Ordinal)
                        && state.ToString()!.Contains("over-limit", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task WriteFile_Binary_SkipsCapture_AndLogsReason()
    {
        var projectDir = CreateTempProject();
        try
        {
            await File.WriteAllBytesAsync(
                Path.Combine(projectDir, "data.bin"),
                [0x00, 0x01, 0x02, 0xFF, 0xFE]);

            var logger = new Mock<ILogger<AgentSession>>();
            var chat = ToolThenDone("write_file", """{"path":"data.bin","content":"text"}""");
            var session = CreateSession(chat, projectDir, logger: logger.Object);

            await DrainAsync(session.SendMessageAsync("overwrite binary"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.Empty(checkpoint.BeforeImages);
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("Before-image skipped", StringComparison.Ordinal)
                        && state.ToString()!.Contains("binary", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public async Task WriteFile_UnsafePath_SkipsCapture_AndLogsReason()
    {
        var projectDir = CreateTempProject();
        try
        {
            var logger = new Mock<ILogger<AgentSession>>();
            var chat = ToolThenDone(
                "write_file",
                """{"path":"../../outside.lua","content":"nope"}""");
            var session = CreateSession(chat, projectDir, logger: logger.Object);

            await DrainAsync(session.SendMessageAsync("escape"));

            var checkpoint = Assert.Single(session.Checkpoints);
            Assert.Empty(checkpoint.BeforeImages);
            logger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) =>
                        state.ToString()!.Contains("Before-image skipped", StringComparison.Ordinal)
                        && state.ToString()!.Contains("unsafe-path", StringComparison.Ordinal)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
        finally
        {
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, true);
        }
    }
}
