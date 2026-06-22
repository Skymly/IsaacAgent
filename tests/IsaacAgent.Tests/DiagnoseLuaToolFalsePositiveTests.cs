using IsaacAgent.Core.Models;
using IsaacAgent.Tools.Implementations;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Tests that DiagnoseLuaTool does NOT produce false positives on common
/// Lua patterns that were incorrectly flagged before the P3-6 fix.
/// </summary>
public class DiagnoseLuaToolFalsePositiveTests
{
    [Fact]
    public void Analyze_BracketsInsideStrings_NoBalanceError()
    {
        // Brackets inside string literals should not be counted.
        var code = """
            local mod = RegisterMod("TestMod", 1)
            local s = "hello ( world"
            local s2 = "another [ string"
            local s3 = "braces } here"
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("Unbalanced"));
    }

    [Fact]
    public void Analyze_BracketsInComments_NoBalanceError()
    {
        // Brackets inside comments should not be counted.
        var code = """
            local mod = RegisterMod("TestMod", 1)
            -- This comment has ( unbalanced parens
            -- And { unbalanced braces
            -- And [ unbalanced brackets
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("Unbalanced"));
    }

    [Fact]
    public void Analyze_EscapedQuotesInString_NoMismatchWarning()
    {
        // Escaped quotes should not cause mismatched quote warnings.
        var code = """
            local mod = RegisterMod("TestMod", 1)
            local s = "hello \"world\""
            local s2 = 'it\'s a test'
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("Mismatched"));
    }

    [Fact]
    public void Analyze_LongString_NoMismatchWarning()
    {
        // Long strings [[...]] don't use quotes and should not trigger mismatch warnings.
        var code = """
            local mod = RegisterMod("TestMod", 1)
            local s = [[hello world]]
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("Mismatched"));
    }

    [Fact]
    public void Analyze_AddCollectibleWithCommaNoSpace_NoFalseWarning()
    {
        // AddCollectible with comma but no space should not trigger the
        // "should specify charge" warning (it has the args, just no space).
        var code = """
            local mod = RegisterMod("TestMod", 1)
            mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
                local player = Isaac.GetPlayer()
                player:AddCollectible(CollectibleType.COLLECTIBLE_SAD_ONION,-1,-1)
            end)
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("AddCollectible"));
    }

    [Fact]
    public void Analyze_GlobalVarCheck_ExcludesCommentedLines()
    {
        // Commented-out assignments should not trigger global variable warnings.
        var code = """
            local mod = RegisterMod("TestMod", 1)
            -- myVar = 42
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("myVar") && d.Message.Contains("global"));
    }

    [Fact]
    public void Analyze_DebugSevenPattern_DetectsActualDebugCommand()
    {
        // The actual debug 7 console command pattern should be detected.
        var code = """
            local mod = RegisterMod("TestMod", 1)
            print("debug 7")
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Info
            && d.Message.Contains("Debug 7"));
    }

    [Fact]
    public void Analyze_DebugSevenPattern_DoesNotFalsePositiveOnRandomPrint()
    {
        // A print statement that happens to contain "debug" and "7" but not
        // as the actual debug command should NOT trigger.
        var code = """
            local mod = RegisterMod("TestMod", 1)
            print("debugging line 7 of the script")
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Info
            && d.Message.Contains("Debug 7"));
    }

    [Fact]
    public void Analyze_BalancedCodeWithStringsAndComments_NoBalanceErrors()
    {
        // Complex code with strings containing brackets and comments should
        // not produce false balance errors.
        var code = """
            local mod = RegisterMod("TestMod", 1)
            -- Configuration table (with parens in comment)
            local config = {
                name = "Test (Mod)",
                values = {1, 2, 3},
                -- [note: brackets in comment]
                callback = function()
                    print("hello (world)")
                end
            }
            mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
                -- do stuff
            end)
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Error
            && d.Message.Contains("Unbalanced"));
    }
}
