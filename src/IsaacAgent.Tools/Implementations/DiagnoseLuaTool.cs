using System.Text.Json;
using System.Text.RegularExpressions;
using IsaacAgent.Core.Knowledge;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Tools.Implementations;

public sealed class DiagnoseLuaTool : ITool
{
    public string Name => "diagnose_lua";
    public string Description => "Analyze a Lua file for common Isaac modding issues: syntax errors, undefined callbacks, incorrect API usage, and missing mod structure.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["path"] = new() { Type = "string", Description = "Relative path to the Lua file to diagnose" }
            },
            Required = ["path"]
        }
    };

    private readonly string _projectDir;

    public DiagnoseLuaTool(string projectDir) => _projectDir = projectDir;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var relPath = args.GetProperty("path").GetString()!;
        var fullPath = Path.GetFullPath(Path.Combine(_projectDir, relPath));

        if (!File.Exists(fullPath))
            return $"Error: File not found: {relPath}";

        var content = await File.ReadAllTextAsync(fullPath, ct);
        var diagnostics = Analyze(content, relPath);

        if (diagnostics.Count == 0)
            return "No issues found. Code looks good!";

        return string.Join('\n', diagnostics.Select(d =>
            $"[{d.Severity}] {d.FilePath}:{d.Line}:{d.Column} — {d.Message}" +
            (d.Suggestion is not null ? $"\n  Suggestion: {d.Suggestion}" : "")));
    }

    public static List<Diagnostic> Analyze(string content, string filePath)
    {
        var diags = new List<Diagnostic>();
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNum = i + 1;

            if (line.Contains("RegisterMod(") && !line.Contains("local") && !line.Contains("mod"))
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = "RegisterMod result should be stored in a local variable",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1,
                    Suggestion = "Use: local mod = RegisterMod(\"MyMod\", 1)"
                });

            var callbackMatch = Regex.Match(line, @"AddCallback\(\s*(ModCallbacks\.)?(\w+)");
            if (callbackMatch.Success)
            {
                var callbackName = callbackMatch.Groups[2].Value;
                if (!ModCallbacks.Callbacks.ContainsKey(callbackName) &&
                    !ModCallbacks.Callbacks.ContainsKey("MC_" + callbackName))
                {
                    diags.Add(new Diagnostic
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Message = $"Unknown callback: {callbackName}",
                        FilePath = filePath,
                        Line = lineNum,
                        Column = callbackMatch.Index + 1,
                        Suggestion = "Check ModCallbacks enum for valid callback names"
                    });
                }
            }

            if (line.Contains("Isaac.GetPlayer()") && line.Contains("==") && !line.Contains("nil"))
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Info,
                    Message = "Isaac.GetPlayer() returns EntityPlayer, not a comparable value",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1
                });

            if (Regex.IsMatch(line, @"player:AddCollectible\([^)]*\)") && !line.Contains(", "))
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = "AddCollectible should specify charge and pool for clarity",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1,
                    Suggestion = "Use: player:AddCollectible(CollectibleType.COLLECTIBLE_X, -1, -1)"
                });

            if (line.Contains("mod:StartNewGame"))
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Error,
                    Message = "StartNewGame is a Game() method, not a mod method",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1,
                    Suggestion = "Use: Game():StartNewGame()"
                });

            if (line.Contains("print(") && line.Contains("debug") && line.Contains("7"))
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Info,
                    Message = "Debug 7 displays damage values in console. Remove for release.",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1
                });
        }

        if (!content.Contains("RegisterMod"))
            diags.Add(new Diagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = "No RegisterMod call found. Every mod needs to register itself.",
                FilePath = filePath,
                Line = 1,
                Column = 1,
                Suggestion = "Add: local mod = RegisterMod(\"MyModName\", 1)"
            });

        var parenBalance = content.Count(c => c == '(') - content.Count(c => c == ')');
        if (parenBalance != 0)
            diags.Add(new Diagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = $"Unbalanced parentheses: {Math.Abs(parenBalance)} {'(' + (parenBalance > 0 ? "" : ")")} unmatched",
                FilePath = filePath,
                Line = lines.Length,
                Column = 1
            });

        return diags;
    }
}
