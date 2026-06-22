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

    public DiagnoseLuaTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var relPath = args.GetProperty("path").GetString()!;
        var fullPath = Path.GetFullPath(Path.Combine(_projectDir, relPath));

        var projectRoot = _projectDir.EndsWith(Path.DirectorySeparatorChar)
            ? _projectDir
            : _projectDir + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            return "Error: Path traversal detected.";

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

        // Strip strings and comments for bracket balancing to avoid false positives.
        var codeOnly = StripStringsAndComments(content);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNum = i + 1;
            var codeLine = StripStringsAndComments(line);

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

            // AddCollectible: check for missing charge/pool arguments.
            // Use codeLine (strings stripped) to avoid matching commas inside string literals.
            var addCollectibleMatch = Regex.Match(codeLine, @"(\w+):AddCollectible\(([^)]*)\)");
            if (addCollectibleMatch.Success)
            {
                var args = addCollectibleMatch.Groups[2].Value;
                // Count top-level commas (no nesting in AddCollectible args typically)
                var commaCount = args.Count(c => c == ',');
                if (commaCount < 2)
                {
                    diags.Add(new Diagnostic
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Message = "AddCollectible should specify charge and pool for clarity",
                        FilePath = filePath,
                        Line = lineNum,
                        Column = addCollectibleMatch.Index + 1,
                        Suggestion = "Use: player:AddCollectible(CollectibleType.COLLECTIBLE_X, -1, -1)"
                    });
                }
            }

            if (codeLine.Contains("mod:StartNewGame"))
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Error,
                    Message = "StartNewGame is a Game() method, not a mod method",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1,
                    Suggestion = "Use: Game():StartNewGame()"
                });

            // Debug 7 console command check — look for the actual console command pattern.
            if (Regex.IsMatch(line, @"(?:print|Isaac\.ConsoleOutput)\s*\(\s*[\""']debug\s+7[\""']"))
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Info,
                    Message = "Debug 7 displays damage values in console. Remove for release.",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1
                });

            // Global variable leak: assignment without local.
            // Use codeLine to avoid matching inside strings/comments.
            // Exclude table field assignments (t.field, t["key"], t:method).
            var globalMatch = Regex.Match(codeLine, @"^\s*(\w+)\s*=\s*[^=]");
            if (globalMatch.Success &&
                !codeLine.Contains("local ") &&
                !codeLine.Contains("function ") &&
                !codeLine.Contains("mod.") &&
                !codeLine.Contains("mod:") &&
                !IsBuiltInGlobal(globalMatch.Groups[1].Value))
            {
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"Possible global variable '{globalMatch.Groups[1].Value}' — should be declared with 'local'",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = globalMatch.Index + 1,
                    Suggestion = "Add 'local' keyword: local " + globalMatch.Value.Trim()
                });
            }

            // Deprecated GetPlayerType
            if (codeLine.Contains("GetPlayerType") && !codeLine.Contains("GetPlayerTypeOf"))
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    Message = "GetPlayerType is deprecated",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1,
                    Suggestion = "Use GetPlayerTypeOf() instead"
                });

            // REPENTOGON API usage — remind to declare dependency
            if (line.Contains("REPENTOGON") && lineNum == 1)
                diags.Add(new Diagnostic
                {
                    Severity = DiagnosticSeverity.Info,
                    Message = "REPENTOGON API used — ensure mod marks REPENTOGON as dependency in metadata.xml",
                    FilePath = filePath,
                    Line = lineNum,
                    Column = 1
                });

            // Mismatched string quotes — use escape-aware counting on the raw line
            // (before comments) to detect unterminated strings.
            // Skip lines with long string brackets [[...]] which don't use quotes.
            if (!line.Contains("[[") && !line.Contains("]]"))
            {
                var commentIdx = FindCommentStart(line);
                var lineBeforeComment = commentIdx >= 0 ? line[..commentIdx] : line;
                var dqCount = CountUnescapedQuotes(lineBeforeComment, '"');
                var sqCount = CountUnescapedQuotes(lineBeforeComment, '\'');
                if (dqCount % 2 != 0 || sqCount % 2 != 0)
                    diags.Add(new Diagnostic
                    {
                        Severity = DiagnosticSeverity.Warning,
                        Message = "Mismatched double quotes on this line",
                        FilePath = filePath,
                        Line = lineNum,
                        Column = 1
                    });
            }
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

        // Bracket balance checks — use codeOnly (strings/comments stripped) to avoid
        // false positives from brackets inside string literals.
        var parenBalance = codeOnly.Count(c => c == '(') - codeOnly.Count(c => c == ')');
        if (parenBalance != 0)
            diags.Add(new Diagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = $"Unbalanced parentheses: {Math.Abs(parenBalance)} {'(' + (parenBalance > 0 ? "" : ")")} unmatched",
                FilePath = filePath,
                Line = lines.Length,
                Column = 1
            });

        var braceBalance = codeOnly.Count(c => c == '{') - codeOnly.Count(c => c == '}');
        if (braceBalance != 0)
            diags.Add(new Diagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = $"Unbalanced braces: {Math.Abs(braceBalance)} {'{' + (braceBalance > 0 ? "" : "}")} unmatched",
                FilePath = filePath,
                Line = lines.Length,
                Column = 1
            });

        var bracketBalance = codeOnly.Count(c => c == '[') - codeOnly.Count(c => c == ']');
        if (bracketBalance != 0)
            diags.Add(new Diagnostic
            {
                Severity = DiagnosticSeverity.Error,
                Message = $"Unbalanced brackets: {Math.Abs(bracketBalance)} {'[' + (bracketBalance > 0 ? "" : "]")} unmatched",
                FilePath = filePath,
                Line = lines.Length,
                Column = 1
            });

        return diags;
    }

    /// <summary>
    /// Count occurrences of a quote character, skipping escaped ones (preceded by \).
    /// </summary>
    private static int CountUnescapedQuotes(string s, char quote)
    {
        var count = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length) { i++; continue; }
            if (s[i] == quote) count++;
        }
        return count;
    }

    /// <summary>
    /// Find the index of a line comment (--...) that's not inside a string.
    /// Returns -1 if no comment found.
    /// </summary>
    private static int FindCommentStart(string line)
    {
        var inString = false;
        var stringChar = '\0';
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (inString)
            {
                if (line[i] == '\\' && i + 1 < line.Length) { i++; continue; }
                if (line[i] == stringChar) inString = false;
                continue;
            }
            if (line[i] == '"' || line[i] == '\'') { inString = true; stringChar = line[i]; continue; }
            if (line[i] == '-' && line[i + 1] == '-') return i;
        }
        return -1;
    }

    /// <summary>
    /// Replace string literals and comments with spaces (preserving line structure)
    /// so that bracket/quote analysis only sees actual code tokens.
    /// </summary>
    private static string StripStringsAndComments(string input)
    {
        var result = new System.Text.StringBuilder(input.Length);
        var i = 0;
        var inString = false;
        var stringChar = '\0';
        var inLongString = false;
        var inLineComment = false;
        var inBlockComment = false;

        while (i < input.Length)
        {
            // Handle line comments (--...)
            if (!inString && !inLongString && !inBlockComment && !inLineComment &&
                i + 1 < input.Length && input[i] == '-' && input[i + 1] == '-')
            {
                // Check for long comment --[[...]] or --[==[...]==]
                if (i + 3 < input.Length && input[i + 2] == '[')
                {
                    var level = 0;
                    var j = i + 3;
                    while (j < input.Length && input[j] == '=') { level++; j++; }
                    if (j < input.Length && input[j] == '[')
                    {
                        inBlockComment = true;
                        i = j + 1;
                        continue;
                    }
                }
                inLineComment = true;
                i += 2;
                continue;
            }

            if (inLineComment)
            {
                if (input[i] == '\n') inLineComment = false;
                result.Append(input[i] == '\n' ? '\n' : ' ');
                i++;
                continue;
            }

            if (inBlockComment)
            {
                // Look for closing ]=...=]
                if (input[i] == ']')
                {
                    var level = 0;
                    var j = i + 1;
                    while (j < input.Length && input[j] == '=') { level++; j++; }
                    if (j < input.Length && input[j] == ']')
                    {
                        inBlockComment = false;
                        for (var k = i; k <= j; k++) result.Append(' ');
                        i = j + 1;
                        continue;
                    }
                }
                result.Append(input[i] == '\n' ? '\n' : ' ');
                i++;
                continue;
            }

            if (inLongString)
            {
                if (input[i] == ']')
                {
                    var level = 0;
                    var j = i + 1;
                    while (j < input.Length && input[j] == '=') { level++; j++; }
                    if (j < input.Length && input[j] == ']')
                    {
                        inLongString = false;
                        for (var k = i; k <= j; k++) result.Append(' ');
                        i = j + 1;
                        continue;
                    }
                }
                result.Append(input[i] == '\n' ? '\n' : ' ');
                i++;
                continue;
            }

            if (inString)
            {
                if (input[i] == '\\' && i + 1 < input.Length)
                {
                    result.Append(' ');
                    result.Append(' ');
                    i += 2;
                    continue;
                }
                if (input[i] == stringChar)
                {
                    inString = false;
                    result.Append(' ');
                    i++;
                    continue;
                }
                result.Append(input[i] == '\n' ? '\n' : ' ');
                i++;
                continue;
            }

            // Not in string/comment — check for string/comment starts
            if (input[i] == '"' || input[i] == '\'')
            {
                inString = true;
                stringChar = input[i];
                result.Append(' ');
                i++;
                continue;
            }

            // Long string [[...]] or [==[...]==]
            if (input[i] == '[')
            {
                var level = 0;
                var j = i + 1;
                while (j < input.Length && input[j] == '=') { level++; j++; }
                if (j < input.Length && input[j] == '[')
                {
                    inLongString = true;
                    for (var k = i; k <= j; k++) result.Append(' ');
                    i = j + 1;
                    continue;
                }
            }

            result.Append(input[i]);
            i++;
        }

        return result.ToString();
    }

    private static bool IsBuiltInGlobal(string name) =>
        name is "mod" or "Isaac" or "Game" or "Input" or "MusicManager" or "Options" or
        "RegisterMod" or "Vector" or "Entity" or "EntityPlayer" or "EntityNPC" or
        "Room" or "Level" or "GridEntity" or "Sprite" or "Font" or "SFX" or
        "NullFrame" or "Color" or "KColor" or "Difficulty" or "LevelStage" or
        "RoomType" or "BackdropType" or "GridEntityType" or "EntityType" or
        "CollectibleType" or "TrinketType" or "CardType" or "PillCardType" or
        "PlayerType" or "ModCallbacks" or "CacheFlag" or "EntityFlag" or
        "EntityPartition" or "StbType" or "StbRouteType" or "NullItemID" or
        "PickupVariant" or "TearVariant" or "EffectVariant" or "FamiliarVariant" or
        "BombVariant" or "ProjectileVariant" or "KnifeVariant" or "LaserVariant" or
        "PressurePlateVariant" or "PoofVariant" or "SuckerVariant" or
        "DeliriumVisionType" or "EventsTable" or "EventCallback" or
        "_" or "self" or "pairs" or "ipairs" or "print" or "tostring" or "tonumber" or
        "type" or "error" or "assert" or "pcall" or "xpcall" or "select" or
        "unpack" or "table" or "string" or "math" or "os" or "io" or "coroutine" or
        "require" or "setmetatable" or "getmetatable" or "rawget" or "rawset" or
        "rawequal" or "rawlen" or "next" or "load" or "loadfile" or "dofile" or
        "collectgarbage" or "arg" or "package" or "_G" or "_VERSION";
}
