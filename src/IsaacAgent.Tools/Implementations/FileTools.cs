using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Tools.Implementations;

public sealed class ReadFileTool : ITool
{
    public string Name => "read_file";
    public string Description => "Read the content of a file in the mod project directory. Use this before modifying existing files to understand the current structure, and when debugging issues reported by diagnose_lua or parse_log.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["path"] = new() { Type = "string", Description = "Relative path to the file from the project root" }
            },
            Required = ["path"]
        }
    };

    private readonly string _projectDir;

    public ReadFileTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var relPath = args.GetProperty("path").GetString()!;
        var (fullPath, isSafe) = FileToolPathSafety.Resolve(_projectDir, relPath);

        if (!isSafe)
            return "Error: Path traversal detected.";

        if (!File.Exists(fullPath))
            return $"Error: File not found: {relPath}";

        return await File.ReadAllTextAsync(fullPath, ct);
    }
}

public sealed class WriteFileTool : ITool
{
    public string Name => "write_file";
    public string Description => "Write content to a file in the mod project directory. Creates parent directories if needed. Use this for creating new files or rewriting entire files. For small changes to large files, prefer diff_apply or batch_edit instead.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["path"] = new() { Type = "string", Description = "Relative path to the file from the project root" },
                ["content"] = new() { Type = "string", Description = "The content to write to the file" }
            },
            Required = ["path", "content"]
        }
    };

    private readonly string _projectDir;

    public WriteFileTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var relPath = args.GetProperty("path").GetString()!;
        var content = args.GetProperty("content").GetString()!;
        var (fullPath, isSafe) = FileToolPathSafety.Resolve(_projectDir, relPath);

        if (!isSafe)
            return "Error: Path traversal detected.";

        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null) Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(fullPath, content, ct);
        return $"File written: {relPath} ({content.Length} chars)";
    }
}

public sealed class ListFilesTool : ITool
{
    public string Name => "list_files";
    public string Description => "List all files in the mod project directory.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["subdir"] = new() { Type = "string", Description = "Optional subdirectory to list (default: root)" }
            },
            Required = []
        }
    };

    private readonly string _projectDir;

    public ListFilesTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var subdir = "";
        if (!string.IsNullOrEmpty(arguments))
        {
            using var doc = JsonDocument.Parse(arguments);
            if (doc.RootElement.TryGetProperty("subdir", out var sd))
                subdir = sd.GetString() ?? "";
        }

        string targetDir;
        if (string.IsNullOrEmpty(subdir))
        {
            targetDir = _projectDir;
        }
        else
        {
            var (resolvedDir, dirSafe) = FileToolPathSafety.Resolve(_projectDir, subdir);
            if (!dirSafe)
                return Task.FromResult("Error: Path traversal detected.");
            targetDir = resolvedDir;
        }

        if (!Directory.Exists(targetDir))
            return Task.FromResult($"Error: Directory not found: {subdir}");

        var files = EnumerateFilesSafe(targetDir)
            .Select(f => Path.GetRelativePath(_projectDir, f).Replace('\\', '/'))
            .ToList();

        return Task.FromResult(files.Count > 0
            ? string.Join('\n', files)
            : "No files found.");
    }

    /// <summary>
    /// Recursively enumerate files, skipping reparse points (junctions/symlinks)
    /// to prevent traversal outside the project directory.
    /// </summary>
    private static IEnumerable<string> EnumerateFilesSafe(string dir)
    {
        var stack = new Stack<string>();
        stack.Push(dir);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            string[] subDirs;
            string[] currentFiles;

            try
            {
                subDirs = Directory.GetDirectories(current);
                currentFiles = Directory.GetFiles(current);
            }
            catch { continue; }

            foreach (var f in currentFiles)
                yield return f;

            foreach (var sd in subDirs)
            {
                // Skip reparse points (junctions, symlinks) to avoid
                // following links outside the project directory.
                if (!IsReparsePoint(sd))
                    stack.Push(sd);
            }
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            var di = new DirectoryInfo(path);
            return di.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch { return false; }
    }
}

internal static class FileToolPathSafety
{
    /// <summary>
    /// Normalizes a relative path by collapsing sequences of 3+ dots
    /// (e.g. "....") into ".." to defeat double-encoded traversal attempts
    /// like "....//target/evil.lua" that <see cref="Path.GetFullPath"/>
    /// treats as a literal directory name rather than a parent reference.
    /// </summary>
    private static string NormalizeRelativePath(string relPath)
    {
        // Replace alternate separators with the OS separator, then split
        // into segments so we can inspect each one independently.
        var normalized = relPath.Replace('/', Path.DirectorySeparatorChar)
                                .Replace('\\', Path.DirectorySeparatorChar);
        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.None);
        for (var i = 0; i < segments.Length; i++)
        {
            // 3+ consecutive dots → treat as parent-directory reference
            if (segments[i].Length >= 3 && segments[i].All(c => c == '.'))
                segments[i] = "..";
        }
        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    /// <summary>
    /// Resolves <paramref name="relPath"/> against <paramref name="projectDir"/>
    /// after normalizing double-encoded traversal patterns, then verifies
    /// the resulting full path stays within the project directory.
    /// </summary>
    public static (string FullPath, bool IsSafe) Resolve(string projectDir, string relPath)
    {
        var normalizedRel = NormalizeRelativePath(relPath);
        var fullPath = Path.GetFullPath(Path.Combine(projectDir, normalizedRel));
        return (fullPath, IsWithinProject(fullPath, projectDir));
    }

    public static bool IsWithinProject(string fullPath, string projectDir)
    {
        var projectRoot = projectDir.EndsWith(Path.DirectorySeparatorChar)
            ? projectDir
            : projectDir + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, projectDir, StringComparison.OrdinalIgnoreCase);
    }
}
