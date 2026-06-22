using IsaacAgent.Core.Models;
using IsaacAgent.Tools.Implementations;
using Xunit;

namespace IsaacAgent.Tests;

public class DiagnoseLuaToolExtendedTests
{
    [Fact]
    public void Analyze_GlobalVariableLeak_ReturnsWarning()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            myVar = 42
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("myVar"));
    }

    [Fact]
    public void Analyze_LocalVariable_NoGlobalLeakWarning()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            local myVar = 42
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Message.Contains("myVar") && d.Message.Contains("global"));
    }

    [Fact]
    public void Analyze_BuiltInGlobal_NoGlobalLeakWarning()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            Isaac.ExecuteCommand("test")
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Message.Contains("Isaac") && d.Message.Contains("global"));
    }

    [Fact]
    public void Analyze_DeprecatedGetPlayerType_ReturnsWarning()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            local pType = player:GetPlayerType()
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("GetPlayerType"));
    }

    [Fact]
    public void Analyze_UnbalancedBraces_ReturnsError()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            local t = {
                key = "value"
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("Unbalanced braces"));
    }

    [Fact]
    public void Analyze_UnbalancedBrackets_ReturnsError()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            local arr = {[1] = "a", [2 = "b"}
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("Unbalanced brackets"));
    }

    [Fact]
    public void Analyze_BalancedCode_NoBalanceErrors()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            local t = {key = "value"}
            local arr = {1, 2, 3}
            mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
                print("ok")
            end)
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("Unbalanced"));
    }

    [Fact]
    public void Analyze_MismatchedQuotes_ReturnsWarning()
    {
        // Use a line with an odd number of double quotes (not inside a comment)
        var code = "local mod = RegisterMod(\"TestMod\", 1)\nlocal s = \"unterminated\n";
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        var quoteDiags = diags.Where(d => d.Message.Contains("Mismatched double quotes")).ToList();
        Assert.NotEmpty(quoteDiags);
    }

    [Fact]
    public void Analyze_StartNewGameOnMod_ReturnsError()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            mod:StartNewGame()
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("StartNewGame"));
    }

    [Fact]
    public void Analyze_AddCollectibleWithoutCharge_ReturnsWarning()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
                local player = Isaac.GetPlayer()
                player:AddCollectible(CollectibleType.COLLECTIBLE_SAD_ONION)
            end)
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("AddCollectible"));
    }

    [Fact]
    public async Task ExecuteAsync_PathTraversal_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_diag_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new DiagnoseLuaTool(tempDir);
            var args = """{"path":"../../../etc/passwd"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Path traversal", result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_diag_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new DiagnoseLuaTool(tempDir);
            var args = """{"path":"nonexistent.lua"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.Contains("File not found", result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
