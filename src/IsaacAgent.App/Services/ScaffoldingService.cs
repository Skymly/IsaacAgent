using System.Security;
using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Tools.Implementations;

namespace IsaacAgent.App.Services;

/// <summary>
/// Thin App façade over mod scaffolding. UI ViewModels call this instead of
/// constructing <see cref="ScaffoldModTool"/> or duplicating template file logic.
/// </summary>
public sealed class ScaffoldingService : IScaffoldingService
{
    public async Task CreateSkeletonAsync(
        string projectDir,
        string name,
        string? description = null,
        string? author = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(projectDir);
        var tool = new ScaffoldModTool(projectDir);
        var args = JsonSerializer.Serialize(new
        {
            name,
            description = description ?? "",
            author = author ?? ""
        });
        await tool.ExecuteAsync(args, ct);
    }

    public async Task<(string[]? Files, string? Error)> ScaffoldFromTemplateAsync(
        string targetDir,
        ModTemplate template,
        string? name = null,
        string? description = null,
        string? author = null,
        CancellationToken ct = default)
    {
        var modName = string.IsNullOrWhiteSpace(name) ? "MyMod" : name;
        var modDescription = string.IsNullOrWhiteSpace(description)
            ? "A custom Binding of Isaac mod"
            : description;
        var modAuthor = string.IsNullOrWhiteSpace(author) ? "Unknown" : author;

        try
        {
            Directory.CreateDirectory(targetDir);

            var created = new List<string>();

            foreach (var dir in template.Directories)
            {
                var fullPath = Path.Combine(targetDir, dir);
                Directory.CreateDirectory(fullPath);
                created.Add(dir + "/");
            }

            foreach (var (relPath, content) in template.Files)
            {
                var fileContent = content
                    .Replace("{name}", EscapeLuaString(modName))
                    .Replace("{description}", SecurityElement.Escape(modDescription) ?? "")
                    .Replace("{author}", SecurityElement.Escape(modAuthor) ?? "");

                var fullPath = Path.Combine(targetDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, fileContent, ct);
                created.Add(relPath);
            }

            return (created.ToArray(), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static string EscapeLuaString(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
