using System.Reflection;
using System.Xml;
using System.Xml.Schema;

namespace IsaacAgent.Rag.Tools;

/// <summary>
/// Validates Isaac mod XML files against embedded XSD schemas.
/// Schemas are embedded from the isaac-xml-validator XSD collection.
/// </summary>
public sealed class XmlValidator
{
    private readonly Dictionary<string, XmlSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);
    private readonly XmlSchemaSet _schemaSet = new();
    private bool _initialized;

    /// <summary>
    /// Maps XML root element names to XSD schema names.
    /// </summary>
    private static readonly Dictionary<string, string> RootToSchema = new(StringComparer.OrdinalIgnoreCase)
    {
        ["metadata"] = "metadata.xsd",
        ["items"] = "items.xsd",
        ["entities"] = "entities2.xsd",
        ["players"] = "players.xsd",
        ["costumes"] = "costumes2.xsd",
        ["pocketitems"] = "pocketitems.xsd",
        ["challenges"] = "challenges.xsd",
        ["bosspools"] = "bosspools.xsd",
        ["stages"] = "stages.xsd",
        ["music"] = "music.xsd",
        ["sounds"] = "sounds.xsd",
        ["backdrops"] = "backdrops.xsd",
        ["curses"] = "curses.xsd",
        ["cutscenes"] = "cutscenes.xsd",
        ["giantbook"] = "giantbook.xsd",
        ["itempools"] = "itempools.xsd",
        ["locusts"] = "locusts.xsd",
        ["nightmares"] = "nightmares.xsd",
        ["playerforms"] = "playerforms.xsd",
        ["recipes"] = "recipes.xsd",
        ["shaders"] = "shaders.xsd",
        ["ambush"] = "ambush.xsd",
        ["babies"] = "babies.xsd",
        ["bosscolors"] = "bosscolors.xsd",
        ["bossoverlays"] = "bossoverlays.xsd",
        ["bossportraits"] = "bossportraits.xsd",
        ["bombcostumes"] = "bombcostumes.xsd",
        ["fxlayers"] = "fxlayers.xsd",
        ["info_display"] = "info_display.xsd",
        ["minibosses"] = "minibosses.xsd",
        ["preload"] = "preload.xsd",
        ["wisps"] = "wisps.xsd",
        ["achievements"] = "achievements.xsd",
    };

    public XmlValidator()
    {
        _schemaSet.XmlResolver = new EmbeddedSchemaResolver();
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        var assembly = Assembly.GetExecutingAssembly();
        var resourcePrefix = "IsaacAgent.Rag.Resources.schemas.";

        foreach (var (rootName, schemaFile) in RootToSchema)
        {
            var resourceName = resourcePrefix + schemaFile.Replace('-', '_');
            // Try exact name first, then search
            var actualName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.Equals(resourceName, StringComparison.Ordinal))
                ?? assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(schemaFile, StringComparison.OrdinalIgnoreCase));

            if (actualName is null) continue;

            using var stream = assembly.GetManifestResourceStream(actualName);
            if (stream is null) continue;
            using var reader = XmlReader.Create(stream);
            var schema = XmlSchema.Read(reader, null);
            if (schema is null) continue;
            _schemas[rootName] = schema;
            _schemaSet.Add(schema);
        }

        _schemaSet.Compile();
    }

    /// <summary>
    /// Validate an XML string against the appropriate XSD schema.
    /// Returns a list of validation errors (empty if valid).
    /// </summary>
    public List<XmlValidationError> Validate(string xmlContent, string? rootElementHint = null)
    {
        EnsureInitialized();
        var errors = new List<XmlValidationError>();

        // Detect root element
        var rootName = DetectRootElement(xmlContent) ?? rootElementHint;
        if (rootName is null || !_schemas.TryGetValue(rootName, out var schema))
        {
            errors.Add(new XmlValidationError(
                0, 0,
                $"Unknown XML root element. Cannot determine schema. " +
                $"Supported roots: {string.Join(", ", _schemas.Keys.OrderBy(k => k))}",
                XmlValidationErrorSeverity.Error));
            return errors;
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = _schemaSet,
            ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings
        };

        settings.Schemas = new XmlSchemaSet();
        settings.Schemas.Add(schema);
        settings.ValidationEventHandler += (sender, e) =>
        {
            var ex = e.Exception as XmlSchemaException;
            var severity = e.Severity == XmlSeverityType.Warning
                ? XmlValidationErrorSeverity.Warning
                : XmlValidationErrorSeverity.Error;
            errors.Add(new XmlValidationError(
                ex?.LineNumber ?? 0,
                ex?.LinePosition ?? 0,
                e.Message,
                severity));
        };

        using var stringReader = new StringReader(xmlContent);
        using var reader = XmlReader.Create(stringReader, settings);
        try
        {
            while (reader.Read()) { }
        }
        catch (XmlException ex)
        {
            errors.Add(new XmlValidationError(ex.LineNumber, ex.LinePosition, ex.Message, XmlValidationErrorSeverity.Error));
        }

        return errors;
    }

    /// <summary>
    /// Validate an XML file from disk.
    /// </summary>
    public List<XmlValidationError> ValidateFile(string filePath)
    {
        if (!File.Exists(filePath))
            return [new XmlValidationError(0, 0, $"File not found: {filePath}", XmlValidationErrorSeverity.Error)];

        var content = File.ReadAllText(filePath);
        return Validate(content);
    }

    private static string? DetectRootElement(string xmlContent)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xmlContent));
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Depth == 0)
                    return reader.LocalName;
            }
        }
        catch { }
        return null;
    }

    public IReadOnlyCollection<string> GetSupportedRootElements()
    {
        EnsureInitialized();
        return _schemas.Keys;
    }
}

public sealed record XmlValidationError(
    int LineNumber,
    int LinePosition,
    string Message,
    XmlValidationErrorSeverity Severity);

public enum XmlValidationErrorSeverity { Warning, Error }

/// <summary>
/// Resolves XSD imports (isaacTypes.xsd) from embedded resources instead of remote URLs.
/// </summary>
file sealed class EmbeddedSchemaResolver : XmlResolver
{
    public override object? GetEntity(Uri? absoluteUri, string? role, Type? ofObjectToReturn)
    {
        if (absoluteUri is null) return null;

        var localPath = absoluteUri.Segments.LastOrDefault()?.Replace('-', '_');
        if (string.IsNullOrEmpty(localPath)) return null;

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(localPath, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null) return null;

        return assembly.GetManifestResourceStream(resourceName);
    }
}
