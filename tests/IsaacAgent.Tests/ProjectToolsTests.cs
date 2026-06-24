using System.Text.Json;
using IsaacAgent.Tools.Implementations;
using Xunit;

namespace IsaacAgent.Tests;

public class ProjectToolsTests : IDisposable
{
    private readonly string _tempDir;

    public ProjectToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"isaac_tools_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            // Clear read-only attributes (git sets them on .git internals)
            ClearReadOnly(_tempDir);
            try { Directory.Delete(_tempDir, true); }
            catch { /* best effort cleanup */ }
        }
    }

    private static void ClearReadOnly(string dir)
    {
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
        }
        foreach (var d in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
        {
            try { File.SetAttributes(d, FileAttributes.Normal); } catch { }
        }
    }

    // === DiffApplyTool ===

    [Fact]
    public async Task DiffApply_AddsLines()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "line1\nline2\nline3\n");
        var tool = new DiffApplyTool(_tempDir);

        var patch = """
            --- a/test.lua
            +++ b/test.lua
            @@ -1,3 +1,4 @@
             line1
            +inserted
             line2
             line3
            """;

        var result = await tool.ExecuteAsync($$"""{"path":"test.lua","patch":{{JsonSerializer.Serialize(patch)}}}""");
        Assert.Contains("Patch applied", result);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("inserted", content);
        Assert.Contains("line1", content);
        Assert.Contains("line2", content);
        Assert.Contains("line3", content);
    }

    [Fact]
    public async Task DiffApply_RemovesLines()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "line1\nline2\nline3\n");
        var tool = new DiffApplyTool(_tempDir);

        var patch = """
            --- a/test.lua
            +++ b/test.lua
            @@ -1,3 +1,2 @@
             line1
            -line2
             line3
            """;

        var result = await tool.ExecuteAsync($$"""{"path":"test.lua","patch":{{JsonSerializer.Serialize(patch)}}}""");
        Assert.Contains("Patch applied", result);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.DoesNotContain("line2", content);
        Assert.Contains("line1", content);
        Assert.Contains("line3", content);
    }

    [Fact]
    public async Task DiffApply_ModifiesLines()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "local x = 1\nlocal y = 2\n");
        var tool = new DiffApplyTool(_tempDir);

        var patch = """
            --- a/test.lua
            +++ b/test.lua
            @@ -1,2 +1,2 @@
             local x = 1
            -local y = 2
            +local y = 42
            """;

        var result = await tool.ExecuteAsync($$"""{"path":"test.lua","patch":{{JsonSerializer.Serialize(patch)}}}""");
        Assert.Contains("Patch applied", result);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("local y = 42", content);
        Assert.DoesNotContain("local y = 2\n", content);
    }

    [Fact]
    public async Task DiffApply_PathTraversal_Rejected()
    {
        var tool = new DiffApplyTool(_tempDir);
        var result = await tool.ExecuteAsync("""{"path":"../../../etc/passwd","patch":""}""");
        Assert.Contains("Path traversal", result);
    }

    [Fact]
    public async Task DiffApply_FileNotFound()
    {
        var tool = new DiffApplyTool(_tempDir);
        var result = await tool.ExecuteAsync("""{"path":"nonexistent.lua","patch":""}""");
        Assert.Contains("File not found", result);
    }

    // === BatchEditTool ===

    [Fact]
    public async Task BatchEdit_AppliesMultipleEdits()
    {
        var file1 = Path.Combine(_tempDir, "a.lua");
        var file2 = Path.Combine(_tempDir, "b.lua");
        await File.WriteAllTextAsync(file1, "local x = 1\n");
        await File.WriteAllTextAsync(file2, "local y = 2\n");
        var tool = new BatchEditTool(_tempDir);

        var result = await tool.ExecuteAsync("""{"edits":[{"path":"a.lua","find":"x = 1","replace":"x = 10"},{"path":"b.lua","find":"y = 2","replace":"y = 20"}]}""");
        Assert.Contains("2 file(s) changed", result);

        var content1 = await File.ReadAllTextAsync(file1);
        var content2 = await File.ReadAllTextAsync(file2);
        Assert.Contains("x = 10", content1);
        Assert.Contains("y = 20", content2);
    }

    [Fact]
    public async Task BatchEdit_FindNotFound_Skips()
    {
        var file1 = Path.Combine(_tempDir, "a.lua");
        await File.WriteAllTextAsync(file1, "local x = 1\n");
        var tool = new BatchEditTool(_tempDir);

        var result = await tool.ExecuteAsync("""{"edits":[{"path":"a.lua","find":"nonexistent","replace":"replaced"}]}""");
        Assert.Contains("Skipped", result);
        Assert.Contains("0 file(s) changed", result);
    }

    [Fact]
    public async Task BatchEdit_PathTraversal_Rejected()
    {
        var tool = new BatchEditTool(_tempDir);
        var result = await tool.ExecuteAsync("""{"edits":[{"path":"../../../etc/passwd","find":"x","replace":"y"}]}""");
        Assert.Contains("Path traversal", result);
    }

    // === RunCommandTool ===

    [Fact]
    public async Task RunCommand_EchoesOutput()
    {
        var tool = new RunCommandTool(_tempDir);
        var cmd = OperatingSystem.IsWindows() ? "echo hello" : "echo hello";
        var result = await tool.ExecuteAsync($$"""{"command":"{{cmd}}"}""");
        Assert.Contains("hello", result);
        Assert.Contains("exit code: 0", result);
    }

    [Fact]
    public async Task RunCommand_BlocksDangerousCommand()
    {
        var tool = new RunCommandTool(_tempDir);
        var result = await tool.ExecuteAsync("""{"command":"rm -rf /"}""");
        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunCommand_Timeout()
    {
        var tool = new RunCommandTool(_tempDir);
        var cmd = OperatingSystem.IsWindows() ? "ping -n 10 127.0.0.1" : "sleep 10";
        var result = await tool.ExecuteAsync($$"""{"command":"{{cmd}}","timeout_seconds":1}""");
        Assert.Contains("timed out", result, StringComparison.OrdinalIgnoreCase);
    }

    // === GitStatusTool ===

    [Fact]
    public async Task GitStatus_NoRepo_ReturnsErrorOrStatus()
    {
        var tool = new GitStatusTool(_tempDir);
        // No git repo initialized — should return error gracefully
        var result = await tool.ExecuteAsync("""{"mode":"status"}""");
        // Either returns error (no git repo) or status output
        Assert.True(result.Contains("Error") || result.Contains("Status") || result.Contains("clean"),
            $"Expected error or status, got: {result}");
    }

    [Fact]
    public async Task GitStatus_WithRepo_ShowsStatus()
    {
        // Init a temp git repo
        if (RunGit(_tempDir, "init") != 0) return; // skip if git not available

        RunGit(_tempDir, "config user.email test@test.com");
        RunGit(_tempDir, "config user.name Test");

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "test.lua"), "local x = 1\n");
        RunGit(_tempDir, "add test.lua");
        RunGit(_tempDir, "commit -m initial");

        var tool = new GitStatusTool(_tempDir);
        var result = await tool.ExecuteAsync("""{"mode":"status"}""");
        Assert.Contains("Status", result);
        Assert.Contains("Recent Commits", result);
    }

    private static int RunGit(string dir, string args)
    {
        try
        {
            var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return -1;
            p.WaitForExit();
            return p.ExitCode;
        }
        catch { return -1; }
    }

    // === DiffApplyTool: additional edge cases ===

    [Fact]
    public async Task DiffApply_MultipleHunks()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "line1\nline2\nline3\nline4\nline5\n");
        var tool = new DiffApplyTool(_tempDir);

        var patch = """
            --- a/test.lua
            +++ b/test.lua
            @@ -1,3 +1,4 @@
             line1
            +inserted1
             line2
             line3
            @@ -4,2 +5,3 @@
             line4
            +inserted2
             line5
            """;

        var result = await tool.ExecuteAsync($$"""{"path":"test.lua","patch":{{JsonSerializer.Serialize(patch)}}}""");
        Assert.Contains("Patch applied", result);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("inserted1", content);
        Assert.Contains("inserted2", content);
    }

    [Fact]
    public async Task DiffApply_ContextMismatch_ReturnsError()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "line1\nline2\nline3\n");
        var tool = new DiffApplyTool(_tempDir);

        var patch = """
            --- a/test.lua
            +++ b/test.lua
            @@ -1,3 +1,3 @@
             WRONG_LINE
            -line2
             line3
            """;

        var result = await tool.ExecuteAsync($$"""{"path":"test.lua","patch":{{JsonSerializer.Serialize(patch)}}}""");
        Assert.Contains("Context mismatch", result);
    }

    [Fact]
    public async Task DiffApply_WindowsLineEndings()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "line1\nline2\nline3\n");
        var tool = new DiffApplyTool(_tempDir);

        // Patch with \r\n line endings
        var patch = "--- a/test.lua\r\n+++ b/test.lua\r\n@@ -1,3 +1,4 @@\r\n line1\r\n+inserted\r\n line2\r\n line3\r\n";

        var result = await tool.ExecuteAsync($$"""{"path":"test.lua","patch":{{JsonSerializer.Serialize(patch)}}}""");
        Assert.Contains("Patch applied", result);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("inserted", content);
    }

    // === BatchEditTool: additional edge cases ===

    [Fact]
    public async Task BatchEdit_ReplacesOnlyFirstOccurrence()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "foo\nbar\nfoo\nbaz\n");
        var tool = new BatchEditTool(_tempDir);

        var result = await tool.ExecuteAsync("""{"edits":[{"path":"test.lua","find":"foo","replace":"qux"}]}""");
        Assert.Contains("Applied", result);

        var content = await File.ReadAllTextAsync(filePath);
        // Only first "foo" should be replaced
        Assert.Contains("qux\nbar\nfoo\nbaz", content);
    }

    [Fact]
    public async Task BatchEdit_ReplaceAll_WhenFlagSet()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "foo\nbar\nfoo\nbaz\n");
        var tool = new BatchEditTool(_tempDir);

        var result = await tool.ExecuteAsync("""{"edits":[{"path":"test.lua","find":"foo","replace":"qux","replace_all":true}]}""");
        Assert.Contains("Applied", result);
        Assert.Contains("all", result);

        var content = await File.ReadAllTextAsync(filePath);
        // Both "foo" should be replaced
        Assert.Contains("qux\nbar\nqux\nbaz", content);
    }

    [Fact]
    public async Task BatchEdit_EmptyFind_ReturnsError()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "content\n");
        var tool = new BatchEditTool(_tempDir);

        var result = await tool.ExecuteAsync("""{"edits":[{"path":"test.lua","find":"","replace":"x"}]}""");
        Assert.Contains("find string is empty", result);

        // File should be unchanged
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Equal("content\n", content);
    }

    [Fact]
    public async Task BatchEdit_MultipleEditsToSameFile()
    {
        var filePath = Path.Combine(_tempDir, "test.lua");
        await File.WriteAllTextAsync(filePath, "local x = 1\nlocal y = 2\n");
        var tool = new BatchEditTool(_tempDir);

        var result = await tool.ExecuteAsync("""{"edits":[{"path":"test.lua","find":"x = 1","replace":"x = 10"},{"path":"test.lua","find":"y = 2","replace":"y = 20"}]}""");
        Assert.Contains("1 file(s) changed", result);

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("x = 10", content);
        Assert.Contains("y = 20", content);
    }

    // === RunCommandTool: additional edge cases ===

    [Fact]
    public async Task RunCommand_NonZeroExitCode()
    {
        var tool = new RunCommandTool(_tempDir);
        var cmd = OperatingSystem.IsWindows() ? "exit 42" : "exit 42";
        var result = await tool.ExecuteAsync($$"""{"command":"{{cmd}}"}""");
        Assert.Contains("exit code: 42", result);
    }

    [Fact]
    public async Task RunCommand_DangerousCommand_NoFalsePositive()
    {
        var tool = new RunCommandTool(_tempDir);
        // "reformat" should NOT be blocked (only "format X:" should be)
        var cmd = OperatingSystem.IsWindows() ? "echo reformat" : "echo reformat";
        var result = await tool.ExecuteAsync($$"""{"command":"{{cmd}}"}""");
        Assert.DoesNotContain("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunCommand_BlocksSudo()
    {
        var tool = new RunCommandTool(_tempDir);
        var result = await tool.ExecuteAsync("""{"command":"sudo rm /etc/passwd"}""");
        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }
}
