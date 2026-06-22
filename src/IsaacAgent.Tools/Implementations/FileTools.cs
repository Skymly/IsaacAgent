using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Tools.Implementations;

public sealed class ReadFileTool : ITool
{
    public string Name => "read_file";
    public string Description => "Read the content of a file in the mod project directory.";

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
        var fullPath = Path.GetFullPath(Path.Combine(_projectDir, relPath));

        if (!FileToolPathSafety.IsWithinProject(fullPath, _projectDir))
            return "Error: Path traversal detected.";

        if (!File.Exists(fullPath))
            return $"Error: File not found: {relPath}";

        return await File.ReadAllTextAsync(fullPath, ct);
    }
}

public sealed class WriteFileTool : ITool
{
    public string Name => "write_file";
    public string Description => "Write content to a file in the mod project directory. Creates parent directories if needed.";

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
        var fullPath = Path.GetFullPath(Path.Combine(_projectDir, relPath));

        if (!FileToolPathSafety.IsWithinProject(fullPath, _projectDir))
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

        var targetDir = string.IsNullOrEmpty(subdir)
            ? _projectDir
            : Path.GetFullPath(Path.Combine(_projectDir, subdir));

        if (!FileToolPathSafety.IsWithinProject(targetDir, _projectDir))
            return Task.FromResult("Error: Path traversal detected.");

        if (!Directory.Exists(targetDir))
            return Task.FromResult($"Error: Directory not found: {subdir}");

        var files = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_projectDir, f).Replace('\\', '/'))
            .ToList();

        return Task.FromResult(files.Count > 0
            ? string.Join('\n', files)
            : "No files found.");
    }
}

file static class FileToolPathSafety
{
    public static bool IsWithinProject(string fullPath, string projectDir)
    {
        var projectRoot = projectDir.EndsWith(Path.DirectorySeparatorChar)
            ? projectDir
            : projectDir + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, projectDir, StringComparison.OrdinalIgnoreCase);
    }
}
