using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Tools.Implementations;

/// <summary>
/// Shows git status, recent log, and diff for the project directory.
/// Gives the agent context about current uncommitted changes.
/// </summary>
public sealed class GitStatusTool : ITool
{
    public string Name => "git_status";
    public string Description => "Show git status, recent commits, and uncommitted diff for the project. Use this to understand the current state of changes before making modifications.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["mode"] = new()
                {
                    Type = "string",
                    Description = "What to show",
                    Enum = ["status", "diff", "diff_staged"]
                }
            },
            Required = []
        }
    };

    private readonly string _projectDir;
    public GitStatusTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var mode = "status";
        if (!string.IsNullOrEmpty(arguments))
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("mode", out var m))
                mode = m.GetString() ?? "status";
        }

        return mode switch
        {
            "diff" => await RunGitAsync("diff", ct),
            "diff_staged" => await RunGitAsync("diff --cached", ct),
            _ => await GetStatusAsync(ct),
        };
    }

    private async Task<string> GetStatusAsync(CancellationToken ct)
    {
        var status = await RunGitAsync("status --short", ct);
        var log = await RunGitAsync("log --oneline -5", ct);
        return $"=== Status ===\n{(string.IsNullOrWhiteSpace(status) ? "(clean)" : status)}\n\n=== Recent Commits ===\n{(string.IsNullOrWhiteSpace(log) ? "(no commits)" : log)}";
    }

    private async Task<string> RunGitAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = _projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Disable git features that could execute arbitrary code via
        // malicious .git/config, hooks, or environment variables.
        psi.Environment["GIT_PAGER"] = "cat";
        psi.Environment["GIT_EDITOR"] = "true";
        psi.Environment["GIT_HOOKS"] = "0";
        psi.Environment["GIT_CONFIG_NOSYSTEM"] = "1";

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return "Error: Failed to start git.";
            await proc.WaitForExitAsync(ct);
            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);
            if (proc.ExitCode != 0)
                return $"Error: {(string.IsNullOrWhiteSpace(stderr) ? $"git exited with code {proc.ExitCode}" : stderr.Trim())}";
            return stdout.TrimEnd();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Error: {ex.Message}";
        }
    }
}

/// <summary>
/// Applies a unified diff patch to a file in the project directory.
/// More precise than write_file for large files with small changes.
/// </summary>
public sealed class DiffApplyTool : ITool
{
    public string Name => "diff_apply";
    public string Description => "Apply a unified diff patch to a file in the project directory. More precise than write_file for large files with small changes. The patch must be in unified diff format with @@ hunks.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["path"] = new() { Type = "string", Description = "Relative path to the file to patch" },
                ["patch"] = new() { Type = "string", Description = "Unified diff patch content (with ---, +++, @@ hunks)" }
            },
            Required = ["path", "patch"]
        }
    };

    private readonly string _projectDir;
    public DiffApplyTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var relPath = args.GetProperty("path").GetString()!;
        var patch = args.GetProperty("patch").GetString()!;
        var fullPath = Path.GetFullPath(Path.Combine(_projectDir, relPath));

        if (!FileToolPathSafety.IsWithinProject(fullPath, _projectDir))
            return Task.FromResult("Error: Path traversal detected.");

        if (!File.Exists(fullPath))
            return Task.FromResult($"Error: File not found: {relPath}");

        try
        {
            var originalLines = File.ReadAllLines(fullPath);
            var patchedLines = ApplyPatch(originalLines, patch);
            File.WriteAllLines(fullPath, patchedLines);
            return Task.FromResult($"Patch applied to {relPath} ({originalLines.Length} → {patchedLines.Count} lines)");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error applying patch: {ex.Message}");
        }
    }

    private static List<string> ApplyPatch(string[] originalLines, string patch)
    {
        // Handle both \n and \r\n line endings in the patch
        var patchLines = patch.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var result = new List<string>(originalLines);

        // Track the cumulative offset caused by previous hunks (lines added
        // minus lines removed). Each hunk's oldStart references the original
        // file, so we must adjust by this offset to find the right position
        // in the result list.
        var lineOffset = 0;

        int i = 0;
        while (i < patchLines.Length)
        {
            var line = patchLines[i];

            // Skip file headers
            if (line.StartsWith("---") || line.StartsWith("+++"))
            {
                i++;
                continue;
            }

            // Parse hunk header: @@ -start,count +start,count @@
            if (line.StartsWith("@@"))
            {
                var match = Regex.Match(line, @"@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@");
                if (!match.Success)
                    throw new FormatException($"Invalid hunk header: {line}");

                var oldStart = int.Parse(match.Groups[1].Value);
                // Convert to 0-based index and adjust for previous hunk offsets
                var oldIndex = (oldStart == 0 ? 0 : oldStart - 1) + lineOffset;

                i++;

                // Collect hunk lines
                var hunkLines = new List<(char Prefix, string Content)>();
                while (i < patchLines.Length)
                {
                    var hl = patchLines[i];
                    if (hl.StartsWith("@@") || hl.StartsWith("---") || hl.StartsWith("+++"))
                        break;
                    if (hl.Length == 0)
                    {
                        // Skip empty lines (artifacts of splitting, not real
                        // patch content). A true empty context line in unified
                        // diff format has a space prefix (" ").
                        i++;
                        continue;
                    }
                    var prefix = hl[0];
                    if (prefix == ' ' || prefix == '-' || prefix == '+')
                    {
                        hunkLines.Add((prefix, hl[1..]));
                    }
                    // Skip other lines (like \ No newline at end of file)
                    i++;
                }

                // Apply hunk
                var resultIndex = oldIndex;
                var added = 0;
                var removed = 0;
                foreach (var (prefix, content) in hunkLines)
                {
                    if (prefix == ' ')
                    {
                        // Context line — verify match
                        if (resultIndex < result.Count && result[resultIndex] == content)
                        {
                            resultIndex++;
                        }
                        else
                        {
                            throw new FormatException(
                                $"Context mismatch at line {resultIndex + 1}: expected '{content}', got '{(resultIndex < result.Count ? result[resultIndex] : "<EOF>")}'");
                        }
                    }
                    else if (prefix == '-')
                    {
                        // Remove line
                        if (resultIndex < result.Count && result[resultIndex] == content)
                        {
                            result.RemoveAt(resultIndex);
                            removed++;
                        }
                        else
                        {
                            throw new FormatException(
                                $"Remove mismatch at line {resultIndex + 1}: expected '{content}', got '{(resultIndex < result.Count ? result[resultIndex] : "<EOF>")}'");
                        }
                    }
                    else if (prefix == '+')
                    {
                        // Add line
                        result.Insert(resultIndex, content);
                        resultIndex++;
                        added++;
                    }
                }

                lineOffset += added - removed;
            }
            else
            {
                i++;
            }
        }

        return result;
    }
}

/// <summary>
/// Applies multiple edits across one or more files in a single call.
/// Each edit is a find-and-replace operation on a specific file.
/// </summary>
public sealed class BatchEditTool : ITool
{
    public string Name => "batch_edit";
    public string Description => "Apply multiple find-and-replace edits across one or more files in a single call. Each edit replaces the first occurrence of 'find' with 'replace' in the specified file. Reduces round-trips when making changes to several files at once. Set replace_all to true to replace every occurrence instead.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["edits"] = new()
                {
                    Type = "array",
                    Description = "Array of edit objects, each with: path (file relative path), find (exact text to find), replace (replacement text), replace_all (optional, default false)"
                }
            },
            Required = ["edits"]
        }
    };

    private readonly string _projectDir;
    public BatchEditTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var edits = args.GetProperty("edits").EnumerateArray().ToList();

        var results = new List<string>();
        var filesChanged = new HashSet<string>();

        foreach (var edit in edits)
        {
            var relPath = edit.GetProperty("path").GetString()!;
            var find = edit.GetProperty("find").GetString()!;
            var replace = edit.GetProperty("replace").GetString()!;
            var replaceAll = edit.TryGetProperty("replace_all", out var ra) && ra.GetBoolean();
            var fullPath = Path.GetFullPath(Path.Combine(_projectDir, relPath));

            if (string.IsNullOrEmpty(find))
            {
                results.Add($"  {relPath}: Error: find string is empty");
                continue;
            }

            if (!FileToolPathSafety.IsWithinProject(fullPath, _projectDir))
            {
                results.Add($"  {relPath}: Error: Path traversal detected");
                continue;
            }

            if (!File.Exists(fullPath))
            {
                results.Add($"  {relPath}: Error: File not found");
                continue;
            }

            var content = File.ReadAllText(fullPath);
            if (!content.Contains(find))
            {
                results.Add($"  {relPath}: Skipped (find text not found)");
                continue;
            }

            var newContent = replaceAll
                ? content.Replace(find, replace)
                : ReplaceFirst(content, find, replace);
            File.WriteAllText(fullPath, newContent);
            filesChanged.Add(relPath);
            results.Add($"  {relPath}: Applied ({find.Length} → {replace.Length} chars{(replaceAll ? ", all" : "")})");
        }

        var summary = $"Batch edit complete: {filesChanged.Count} file(s) changed, {edits.Count - filesChanged.Count} skipped/failed.\n{string.Join('\n', results)}";
        return Task.FromResult(summary);
    }

    private static string ReplaceFirst(string text, string search, string replacement)
    {
        var index = text.IndexOf(search, StringComparison.Ordinal);
        return index < 0 ? text : text[..index] + replacement + text[(index + search.Length)..];
    }
}

/// <summary>
/// Runs a shell command in the project directory with safety constraints.
/// </summary>
public sealed class RunCommandTool : ITool
{
    public string Name => "run_command";
    public string Description => "Run a shell command in the project directory. Useful for running lua syntax checks, git operations, or build commands. Commands are executed with a 30-second timeout. Output (stdout + stderr) is returned.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["command"] = new() { Type = "string", Description = "The command to execute" },
                ["timeout_seconds"] = new() { Type = "integer", Description = "Timeout in seconds (default: 30, max: 120)" }
            },
            Required = ["command"]
        }
    };

    private readonly string _projectDir;
    public RunCommandTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var command = args.GetProperty("command").GetString()!;
        var timeoutSec = 30;
        if (args.TryGetProperty("timeout_seconds", out var ts))
            timeoutSec = Math.Clamp(ts.GetInt32(), 1, 120);

        // Block dangerous commands
        if (IsDangerousCommand(command))
            return $"Error: Command blocked for safety: contains a potentially destructive operation.";

        var isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd" : "/bin/sh",
            Arguments = isWindows ? $"/c {command}" : $"-c {command}",
            WorkingDirectory = _projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return "Error: Failed to start process.";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

            try
            {
                await proc.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Kill the process tree to prevent zombie processes
                try { proc.Kill(entireProcessTree: true); } catch { }
                return $"Error: Command timed out after {timeoutSec}s.";
            }

            var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
            var stderr = await proc.StandardError.ReadToEndAsync(ct);

            var output = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(stdout))
                output.AppendLine(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr))
                output.AppendLine($"[stderr]\n{stderr.TrimEnd()}");
            output.AppendLine($"[exit code: {proc.ExitCode}]");

            return output.ToString().TrimEnd();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static readonly Regex[] DangerousPatterns =
    [
        Compile(@"\brm\s+-rf\b"),
        Compile(@"\brmdir\s+/s\b"),
        Compile(@"\bformat\s+[a-z]:"),
        Compile(@"\bdel\s+/f\s+/s\b"),
        Compile(@"\bmkfs\b"),
        Compile(@"\bdd\s+if="),
        Compile(@"\b(shutdown|reboot|halt)\b"),
        Compile(@":\(\)\s*\{"),
        Compile(@"\bfork\s+bomb\b"),
        Compile(@"\bgit\s+push\s+(-f|--force)\b"),
        Compile(@"\b(drop\s+table|drop\s+database|truncate)\b"),
        Compile(@"\bchmod\s+777\s+/"),
        Compile(@"\bsudo\b"),
        Compile(@"\bcurl\s+.*\|\s*(bash|sh)\b"),
        Compile(@"\bwget\s+.*\|\s*(bash|sh)\b"),
    ];

    private static Regex Compile(string pattern) => new(pattern, RegexOptions.IgnoreCase);

    private static bool IsDangerousCommand(string command)
    {
        // Normalize whitespace to prevent bypass via double spaces or tabs
        var normalized = System.Text.RegularExpressions.Regex.Replace(command, @"\s+", " ").Trim().ToLowerInvariant();
        return DangerousPatterns.Any(p => p.IsMatch(normalized));
    }
}
