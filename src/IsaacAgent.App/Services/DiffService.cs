using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.Services;

/// <summary>
/// A single line in a diff view (added, removed, or context).
/// </summary>
public sealed class DiffLine
{
    public enum LineType { Context, Added, Removed, Header }

    public LineType Type { get; init; }
    public string Content { get; init; } = "";
    public int? OldLineNumber { get; init; }
    public int? NewLineNumber { get; init; }

    public string TypeLabel => Type switch
    {
        LineType.Added => "+",
        LineType.Removed => "-",
        LineType.Header => "@",
        _ => " "
    };
}

/// <summary>
/// A hunk of changes in a single file diff.
/// </summary>
public sealed record DiffFile
{
    public string FilePath { get; init; } = "";
    public string OldPath { get; init; } = "";
    public bool IsNew { get; init; }
    public bool IsDeleted { get; init; }
    public List<DiffLine> Lines { get; init; } = [];

    public int AddedCount => Lines.Count(l => l.Type == DiffLine.LineType.Added);
    public int RemovedCount => Lines.Count(l => l.Type == DiffLine.LineType.Removed);
    public string Summary => $"{FilePath} (+{AddedCount} -{RemovedCount})";
}

/// <summary>
/// Service that runs git diff and parses the output into structured diff data.
/// </summary>
public sealed partial class DiffService : ObservableObject
{
    private readonly ILogger<DiffService>? _logger;

    [ObservableProperty]
    private bool _hasChanges;

    [ObservableProperty]
    private string _statusText = "No changes";

    public ObservableCollection<DiffFile> Files { get; } = [];

    public DiffService(ILogger<DiffService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load git diff for the given project directory.
    /// </summary>
    public async Task LoadDiffAsync(string projectDir, CancellationToken ct = default)
    {
        Files.Clear();
        HasChanges = false;

        try
        {
            var diffOutput = await RunGitAsync(projectDir, "diff --no-color", ct);
            if (string.IsNullOrWhiteSpace(diffOutput))
            {
                // Check for staged/untracked files
                var statusOutput = await RunGitAsync(projectDir, "status --short", ct);
                if (string.IsNullOrWhiteSpace(statusOutput))
                {
                    StatusText = "No changes";
                    return;
                }
                StatusText = "Untracked/staged files (no unstaged diff)";
                HasChanges = true;
                return;
            }

            ParseDiff(diffOutput);
            HasChanges = Files.Count > 0;
            StatusText = HasChanges ? $"{Files.Count} file(s) changed" : "No changes";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load diff");
            StatusText = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Load diff of a specific file.
    /// </summary>
    public async Task LoadFileDiffAsync(string projectDir, string filePath, CancellationToken ct = default)
    {
        Files.Clear();
        HasChanges = false;

        try
        {
            var diffOutput = await RunGitAsync(projectDir, $"diff --no-color -- {filePath}", ct);
            if (string.IsNullOrWhiteSpace(diffOutput))
            {
                StatusText = "No changes in this file";
                return;
            }

            ParseDiff(diffOutput);
            HasChanges = Files.Count > 0;
            StatusText = HasChanges ? "File has changes" : "No changes";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load file diff");
            StatusText = $"Error: {ex.Message}";
        }
    }

    private static async Task<string> RunGitAsync(string projectDir, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi);
        if (proc is null) return "";
        await proc.WaitForExitAsync(ct);
        return await proc.StandardOutput.ReadToEndAsync(ct);
    }

    private void ParseDiff(string diffOutput)
    {
        DiffFile? currentFile = null;
        var oldLineNum = 0;
        var newLineNum = 0;

        var lines = diffOutput.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // File header
            if (line.StartsWith("diff --git"))
            {
                if (currentFile is not null)
                    Files.Add(currentFile);
                currentFile = new DiffFile();
                continue;
            }

            if (currentFile is null) continue;

            if (line.StartsWith("--- "))
            {
                var path = line[4..];
                currentFile = currentFile with { OldPath = path == "/dev/null" ? "" : path };
                if (path == "/dev/null") currentFile = currentFile with { IsNew = true };
                continue;
            }

            if (line.StartsWith("+++ "))
            {
                var path = line[4..];
                if (path != "/dev/null")
                    currentFile = currentFile with { FilePath = path[2..] }; // Remove b/ prefix
                else
                    currentFile = currentFile with { IsDeleted = true };
                continue;
            }

            // Hunk header
            if (line.StartsWith("@@"))
            {
                var match = HunkHeaderRegex().Match(line);
                if (match.Success)
                {
                    oldLineNum = int.Parse(match.Groups[1].Value) - 1;
                    newLineNum = int.Parse(match.Groups[3].Value) - 1;
                }
                currentFile.Lines.Add(new DiffLine { Type = DiffLine.LineType.Header, Content = line });
                continue;
            }

            // Added line
            if (line.StartsWith('+') && !line.StartsWith("+++"))
            {
                newLineNum++;
                currentFile.Lines.Add(new DiffLine
                {
                    Type = DiffLine.LineType.Added,
                    Content = line[1..],
                    NewLineNumber = newLineNum
                });
                continue;
            }

            // Removed line
            if (line.StartsWith('-') && !line.StartsWith("---"))
            {
                oldLineNum++;
                currentFile.Lines.Add(new DiffLine
                {
                    Type = DiffLine.LineType.Removed,
                    Content = line[1..],
                    OldLineNumber = oldLineNum
                });
                continue;
            }

            // Context line
            if (line.StartsWith(' '))
            {
                oldLineNum++;
                newLineNum++;
                currentFile.Lines.Add(new DiffLine
                {
                    Type = DiffLine.LineType.Context,
                    Content = line[1..],
                    OldLineNumber = oldLineNum,
                    NewLineNumber = newLineNum
                });
                continue;
            }
        }

        if (currentFile is not null)
            Files.Add(currentFile);
    }

    [GeneratedRegex(@"@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.Compiled)]
    private static partial Regex HunkHeaderRegex();
}
