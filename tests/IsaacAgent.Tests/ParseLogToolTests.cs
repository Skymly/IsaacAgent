using IsaacAgent.Rag.Tools;
using Xunit;

namespace IsaacAgent.Tests;

public class ParseLogToolTests
{
    private static readonly string SampleLog = """
        Binding of Isaac: Repentance v1.7.9b
        Loading mod: TestMod
        [INFO] - Mod loaded successfully
        [INFO] - [warn] item pool ran out of repicks
        Error calling callback (PostUpdate)
        Lua Error: [string "main.lua"]:42: attempt to index nil value (local 'player')
        Error running function: PostUpdate
        Lua Error: [string "scripts/utils.lua"]:15: attempt to call method 'GetCollectible' (a nil value)
        [INFO] - Game started
        [INFO] - could not find entity: 123
        Binding of Isaac: Repentance v1.7.9b
        Loading mod: TestMod
        [INFO] - Mod loaded successfully
        Error calling callback (PostNewRoom)
        Lua Error: [string "main.lua"]:87: attempt to perform arithmetic on a nil value (local 'x')
        Deprecation warning: Use of 'GetPlayerType' is deprecated, use 'GetPlayerTypeOf' instead
        """;

    [Fact]
    public void ParseLog_ErrorsFilter_ReturnsOnlyErrors()
    {
        var entries = ParseLogTool.ParseLog(SampleLog, "errors");

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal(LogEntryType.Error, e.Type));
        Assert.Contains(entries, e => e.Message.Contains("attempt to index nil"));
        Assert.Contains(entries, e => e.Message.Contains("attempt to call method"));
        Assert.Contains(entries, e => e.Message.Contains("attempt to perform arithmetic"));
    }

    [Fact]
    public void ParseLog_ExtractsSourceFileAndLine()
    {
        var entries = ParseLogTool.ParseLog(SampleLog, "errors");

        var firstError = entries.First(e => e.Message.Contains("main.lua"));
        Assert.Equal("main.lua", firstError.SourceFile);
        Assert.Equal(42, firstError.SourceLine);

        var utilsError = entries.First(e => e.Message.Contains("utils.lua"));
        Assert.Equal("scripts/utils.lua", utilsError.SourceFile);
        Assert.Equal(15, utilsError.SourceLine);
    }

    [Fact]
    public void ParseLog_ExtractsCallbackContext()
    {
        var entries = ParseLogTool.ParseLog(SampleLog, "errors");

        Assert.Contains(entries, e => e.Callback is not null);
    }

    [Fact]
    public void ParseLog_WarningsFilter_ReturnsOnlyWarnings()
    {
        var entries = ParseLogTool.ParseLog(SampleLog, "warnings");

        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal(LogEntryType.Warning, e.Type));
    }

    [Fact]
    public void ParseLog_AllFilter_ReturnsEverything()
    {
        var entries = ParseLogTool.ParseLog(SampleLog, "all");

        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.Type == LogEntryType.Error);
        Assert.Contains(entries, e => e.Type == LogEntryType.Warning);
        // Info entries include "item pool ran out" and "could not find" patterns
        Assert.Contains(entries, e => e.Type == LogEntryType.Info);
    }

    [Fact]
    public void ParseLog_LastRunFilter_ReturnsOnlyLastSession()
    {
        var entries = ParseLogTool.ParseLog(SampleLog, "last_run");

        // Last session has 1 error (line 87) + 1 warning
        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.Message.Contains("arithmetic on a nil"));
        // Should NOT contain errors from the first session
        Assert.DoesNotContain(entries, e => e.Message.Contains("attempt to index nil"));
    }

    [Fact]
    public void ParseLog_EmptyLog_ReturnsEmpty()
    {
        var entries = ParseLogTool.ParseLog("", "errors");
        Assert.Empty(entries);
    }

    [Fact]
    public void ParseLog_NoErrors_ReturnsEmpty()
    {
        var log = """
            Binding of Isaac: Repentance v1.7.9b
            [INFO] - Mod loaded successfully
            [INFO] - Game started
            """;

        var entries = ParseLogTool.ParseLog(log, "errors");
        Assert.Empty(entries);
    }

    [Fact]
    public async Task ParseLogTool_FileNotFound_ReturnsHelpfulMessage()
    {
        var tool = new ParseLogTool(Path.GetTempPath());
        var args = System.Text.Json.JsonSerializer.Serialize(new { file_path = "nonexistent_log.txt" });
        var result = await tool.ExecuteAsync(args);

        Assert.Contains("Could not find", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file_path", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParseLogTool_ValidFile_ReturnsParsedErrors()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_log_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var logPath = Path.Combine(tempDir, "log.txt");
        await File.WriteAllTextAsync(logPath, SampleLog);

        try
        {
            var tool = new ParseLogTool(tempDir);
            var args = System.Text.Json.JsonSerializer.Serialize(new { file_path = "log.txt", filter = "errors" });
            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Lua Error", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("main.lua", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("42", result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ParseLogTool_AbsolutePath_Rejected()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_log_abs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new ParseLogTool(tempDir);
            var absPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "some_abs_file.txt"));
            var args = System.Text.Json.JsonSerializer.Serialize(new { file_path = absPath });
            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Absolute paths are not allowed", result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ParseLogTool_PathTraversal_ReturnsError()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"isaac_log_traversal_{Guid.NewGuid():N}");
        var safeDir = Path.Combine(baseDir, "myproject");
        var evilDir = Path.Combine(baseDir, "myproject_evil");
        Directory.CreateDirectory(safeDir);
        Directory.CreateDirectory(evilDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(evilDir, "log.txt"), "secret log content");

            var tool = new ParseLogTool(safeDir);
            var args = System.Text.Json.JsonSerializer.Serialize(new { file_path = "../myproject_evil/log.txt" });
            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Path traversal", result);
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task ParseLogTool_SiblingPrefix_ReturnsError()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"isaac_log_prefix_{Guid.NewGuid():N}");
        var safeDir = Path.Combine(baseDir, "myproject");
        var evilDir = Path.Combine(baseDir, "myproject_evil");
        Directory.CreateDirectory(safeDir);
        Directory.CreateDirectory(evilDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(evilDir, "log.txt"), "secret");

            var tool = new ParseLogTool(safeDir);
            var args = System.Text.Json.JsonSerializer.Serialize(new { file_path = "../myproject_evil/log.txt" });
            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Path traversal", result);
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }
}
