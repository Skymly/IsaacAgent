using IsaacAgent.Core.Models;
using IsaacAgent.Tools.Implementations;
using Xunit;

namespace IsaacAgent.Tests;

public class DiagnosticTests
{
    [Fact]
    public void Analyze_MissingRegisterMod_ReturnsError()
    {
        var code = "print('hello')";
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("RegisterMod"));
    }

    [Fact]
    public void Analyze_ValidCode_NoErrors()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)

            mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
                print("Game started!")
            end)
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "main.lua");

        Assert.DoesNotContain(diags, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Analyze_UnknownCallback_ReturnsWarning()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1)
            mod:AddCallback(ModCallbacks.MC_FAKE_CALLBACK, function() end)
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("MC_FAKE_CALLBACK"));
    }

    [Fact]
    public void Analyze_UnbalancedParens_ReturnsError()
    {
        var code = """
            local mod = RegisterMod("TestMod", 1
            """;
        var diags = DiagnoseLuaTool.Analyze(code, "test.lua");

        Assert.Contains(diags, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("Unbalanced"));
    }
}
