using System.Text.Json;
using System.Text.RegularExpressions;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Rag.Tools;

public sealed class ParseLogTool : ITool
{
    private readonly string _projectDir;

    public ParseLogTool(string projectDir)
    {
        _projectDir = projectDir;
    }

    public string Name => "parse_log";
    public string Description => "Parse the Isaac game log.txt file to extract Lua errors, warnings, and useful diagnostic information. Can read from the default Isaac log location or a custom path. Identifies error lines, file names, line numbers, and callback context.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["file_path"] = new() { Type = "string", Description = "Path to log.txt. If relative, resolves from project dir. If omitted, tries the default Isaac log location (Documents/My Games/Binding of Isaac Repentance/log.txt)." },
                ["filter"] = new() { Type = "string", Description = "Filter results: 'errors' (default), 'warnings', 'all', or 'last_run' (only lines from the most recent game session)." }
            }
        }
    };

    public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var filter = args.TryGetProperty("filter", out var f) ? f.GetString() ?? "errors" : "errors";

        var filePath = ResolveLogPath(args);
        if (filePath is null || !File.Exists(filePath))
        {
            return Task.FromResult(
                $"Could not find log.txt. Searched:\n" +
                $"  - Project-relative path\n" +
                $"  - Default Isaac location: {GetDefaultLogPath()}\n" +
                $"Provide a 'file_path' argument pointing to your log.txt file.");
        }

        var content = File.ReadAllText(filePath);
        var entries = ParseLog(content, filter);

        if (entries.Count == 0)
            return Task.FromResult($"No matching log entries found in '{filePath}' (filter: {filter}).");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {entries.Count} log entries in '{filePath}' (filter: {filter}):\n");

        foreach (var e in entries)
        {
            sb.AppendLine($"  [Line {e.LineNumber}] [{e.Type}] {e.Message}");
            if (e.SourceFile is not null)
                sb.AppendLine($"    File: {e.SourceFile}:{e.SourceLine}");
            if (e.Callback is not null)
                sb.AppendLine($"    Callback: {e.Callback}");
            sb.AppendLine();
        }

        sb.AppendLine("Tips:");
        sb.AppendLine("- Use search_knowledge to find documentation for the API mentioned in errors");
        sb.AppendLine("- Use diagnose_lua to check the Lua file mentioned in the error");

        return Task.FromResult(sb.ToString());
    }

    private string? ResolveLogPath(JsonElement args)
    {
        if (args.TryGetProperty("file_path", out var fp) && fp.ValueKind == JsonValueKind.String)
        {
            var path = fp.GetString()!;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(_projectDir, path);
            return path;
        }
        return GetDefaultLogPath();
    }

    private static string? GetDefaultLogPath()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var path = Path.Combine(docs, "My Games", "Binding of Isaac Repentance", "log.txt");
        return File.Exists(path) ? path : null;
    }

    public static List<LogEntry> ParseLog(string content, string filter)
    {
        var entries = new List<LogEntry>();
        var lines = content.Split('\n');
        var sessionSeparatorLine = -1;

        // Find the last session separator (Isaac writes a separator on game launch)
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (IsSessionSeparator(lines[i]))
            {
                sessionSeparatorLine = i;
                break;
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.Trim();

            if (filter == "last_run" && sessionSeparatorLine >= 0 && i < sessionSeparatorLine)
                continue;

            var entry = ClassifyLine(i + 1, trimmed);
            if (entry is null) continue;

            if (filter == "errors" && entry.Type != LogEntryType.Error)
                continue;
            if (filter == "warnings" && entry.Type != LogEntryType.Warning)
                continue;

            // Try to find callback context from nearby lines
            if (entry.SourceFile is null)
                ExtractSourceInfo(entry, lines, i);

            if (entry.Callback is null)
                ExtractCallback(entry, lines, i);

            entries.Add(entry);
        }

        return entries;
    }

    private static bool IsSessionSeparator(string line) =>
        line.Contains("Binding of Isaac: Repentance", StringComparison.OrdinalIgnoreCase) &&
        (line.Contains("Version", StringComparison.OrdinalIgnoreCase) ||
         line.Contains("v1.", StringComparison.OrdinalIgnoreCase));

    private static LogEntry? ClassifyLine(int lineNumber, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        // Lua Error patterns
        if (line.Contains("Lua Error", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Error running function", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Error calling callback", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("attempt to", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
        {
            return new LogEntry(lineNumber, LogEntryType.Error, line);
        }

        // Warning patterns
        if (line.Contains("[warn]", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("warning:", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("deprecat", StringComparison.OrdinalIgnoreCase))
        {
            return new LogEntry(lineNumber, LogEntryType.Warning, line);
        }

        // Info patterns that are useful for debugging
        if (line.Contains("item pool ran out", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("could not find", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("failed to", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return new LogEntry(lineNumber, LogEntryType.Info, line);
        }

        return null;
    }

    private static void ExtractSourceInfo(LogEntry entry, string[] lines, int currentIndex)
    {
        // Pattern: [string "main.lua"]:42: or [string "scripts/foo.lua"]:123:
        var match = Regex.Match(entry.Message, @"\[string\s+[""']([^""']+)[""']\]:(\d+)");
        if (match.Success)
        {
            entry.SourceFile = match.Groups[1].Value;
            entry.SourceLine = int.Parse(match.Groups[2].Value);
            return;
        }

        // Also check surrounding lines for source info
        for (var i = Math.Max(0, currentIndex - 2); i <= Math.Min(lines.Length - 1, currentIndex + 2); i++)
        {
            match = Regex.Match(lines[i], @"\[string\s+[""']([^""']+)[""']\]:(\d+)");
            if (match.Success)
            {
                entry.SourceFile = match.Groups[1].Value;
                entry.SourceLine = int.Parse(match.Groups[2].Value);
                return;
            }
        }
    }

    private static void ExtractCallback(LogEntry entry, string[] lines, int currentIndex)
    {
        // Look for callback name in nearby lines
        for (var i = Math.Max(0, currentIndex - 3); i <= Math.Min(lines.Length - 1, currentIndex + 3); i++)
        {
            var line = lines[i];
            // Pattern: "Error calling callback (PostUpdate)" or "MC_POST_UPDATE"
            var match = Regex.Match(line, @"(?:callback\s*\(|MC_\w+|PostUpdate|PostRender|PostGameStarted|PostNewRoom|PostNewLevel|EvaluateCache|GetCollectible|UseItem|AddCallback)");
            if (match.Success)
            {
                entry.Callback = match.Value;
                return;
            }
        }
    }
}

public sealed class LogEntry
{
    public int LineNumber { get; init; }
    public LogEntryType Type { get; init; }
    public string Message { get; init; }
    public string? SourceFile { get; set; }
    public int? SourceLine { get; set; }
    public string? Callback { get; set; }

    public LogEntry(int lineNumber, LogEntryType type, string message)
    {
        LineNumber = lineNumber;
        Type = type;
        Message = message;
    }
}

public enum LogEntryType { Error, Warning, Info }
