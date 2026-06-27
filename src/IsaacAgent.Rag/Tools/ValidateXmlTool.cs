using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Rag.Tools;

public sealed class ValidateXmlTool : ITool
{
    private readonly XmlValidator _validator;
    private readonly string _projectDir;

    public ValidateXmlTool(string projectDir)
    {
        _projectDir = Path.GetFullPath(projectDir);
        _validator = new XmlValidator();
    }

    public string Name => "validate_xml";
    public string Description => "Validate an Isaac mod XML file (metadata.xml, items.xml, entities2.xml, players.xml, trinkets.xml, challenges.xml, etc.) against the official XSD schemas. Detects missing required attributes, invalid values, and structural errors. Always call this after creating or modifying any XML file before suggesting the user test in-game.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["file_path"] = new() { Type = "string", Description = "Relative path to the XML file within the project (e.g., 'metadata.xml', 'content/items.xml')" }
            },
            Required = ["file_path"]
        }
    };

    public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var relativePath = args.GetProperty("file_path").GetString()!;

        var fullPath = Path.GetFullPath(Path.Combine(_projectDir, relativePath));
        var projectRoot = _projectDir.EndsWith(Path.DirectorySeparatorChar)
            ? _projectDir
            : _projectDir + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult("Error: Path traversal detected.");
        var errors = _validator.ValidateFile(fullPath);

        if (errors.Count == 0)
            return Task.FromResult($"XML file '{relativePath}' is valid. No errors found.");

        var sb = new System.Text.StringBuilder();
        var errorCount = errors.Count(e => e.Severity == XmlValidationErrorSeverity.Error);
        var warnCount = errors.Count(e => e.Severity == XmlValidationErrorSeverity.Warning);
        sb.AppendLine($"Found {errorCount} error(s) and {warnCount} warning(s) in '{relativePath}':\n");

        foreach (var e in errors.OrderBy(e => e.LineNumber))
        {
            var icon = e.Severity == XmlValidationErrorSeverity.Error ? "ERROR" : "WARN";
            sb.AppendLine($"  [{icon}] Line {e.LineNumber}:{e.LinePosition} — {e.Message}");
        }

        sb.AppendLine();
        sb.AppendLine("Tips:");
        sb.AppendLine("- Check the Isaac modding wiki for correct XML structure");
        sb.AppendLine("- Use search_knowledge to find documentation for this XML file type");

        return Task.FromResult(sb.ToString());
    }
}
