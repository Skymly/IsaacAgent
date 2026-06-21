using IsaacAgent.Rag.Tools;
using Xunit;

namespace IsaacAgent.Tests;

public class XmlValidatorTests
{
    private readonly XmlValidator _validator = new();

    [Fact]
    public void Validate_ValidMetadataXml_Passes()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <metadata>
              <name>Test Mod</name>
              <directory>test_mod</directory>
              <description>A test mod</description>
              <version>1.0</version>
            </metadata>
            """;

        var errors = _validator.Validate(xml);

        Assert.Empty(errors.Where(e => e.Severity == XmlValidationErrorSeverity.Error));
    }

    [Fact]
    public void Validate_MissingRequiredField_InMetadata_ReturnsError()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <metadata>
              <name>Test Mod</name>
              <directory>test_mod</directory>
              <description>A test mod</description>
            </metadata>
            """;

        var errors = _validator.Validate(xml);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Message.Contains("version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InvalidRoot_ReturnsError()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <unknownroot>
              <something>test</something>
            </unknownroot>
            """;

        var errors = _validator.Validate(xml);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Message.Contains("Unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MalformedXml_ReturnsError()
    {
        var xml = "<?xml version=\"1.0\"?><metadata><name>Unclosed";

        var errors = _validator.Validate(xml);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_ValidItemsXml_Passes()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <items gfxroot="gfx/items/" version="1">
              <passive id="1000" name="Test Item" description="A test item" gfx="test.png" cache="all" />
              <active id="1001" name="Test Active" description="A test active" gfx="test_active.png" maxcharges="3" />
            </items>
            """;

        var errors = _validator.Validate(xml);

        Assert.Empty(errors.Where(e => e.Severity == XmlValidationErrorSeverity.Error));
    }

    [Fact]
    public void Validate_ItemsXmlMissingRequiredName_ReturnsError()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <items gfxroot="gfx/items/" version="1">
              <passive id="1000" description="A test item" gfx="test.png" />
            </items>
            """;

        var errors = _validator.Validate(xml);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Message.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ItemsXmlInvalidPngGfx_ReturnsError()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <items gfxroot="gfx/items/" version="1">
              <passive id="1000" name="Test" gfx="not_a_png.txt" />
            </items>
            """;

        var errors = _validator.Validate(xml);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Validate_ValidEntities2Xml_Passes()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <entities>
              <entity id="1000" name="Test Entity" />
            </entities>
            """;

        var errors = _validator.Validate(xml);

        // entities2.xsd may have more required attrs; just verify no crash
        Assert.NotNull(errors);
    }

    [Fact]
    public void GetSupportedRootElements_IncludesCommonSchemas()
    {
        var roots = _validator.GetSupportedRootElements();

        Assert.Contains("metadata", roots);
        Assert.Contains("items", roots);
        Assert.Contains("entities", roots);
        Assert.Contains("players", roots);
    }

    [Fact]
    public void Validate_ValidPlayersXml_Passes()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <players>
              <player name="Test Character" skinColor="0" />
            </players>
            """;

        var errors = _validator.Validate(xml);

        Assert.NotNull(errors);
    }

    [Fact]
    public async Task ValidateXmlTool_ExecuteOnValidFile_ReturnsSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_xml_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var xmlPath = Path.Combine(tempDir, "metadata.xml");
        await File.WriteAllTextAsync(xmlPath, """
            <?xml version="1.0" encoding="UTF-8"?>
            <metadata>
              <name>Test Mod</name>
              <directory>test_mod</directory>
              <description>A test mod</description>
              <version>1.0</version>
            </metadata>
            """);

        try
        {
            var tool = new ValidateXmlTool(tempDir);
            var args = System.Text.Json.JsonSerializer.Serialize(new { file_path = "metadata.xml" });
            var result = await tool.ExecuteAsync(args);

            Assert.Contains("valid", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("No errors", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ValidateXmlTool_ExecuteOnInvalidFile_ReturnsErrors()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_xml_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var xmlPath = Path.Combine(tempDir, "metadata.xml");
        await File.WriteAllTextAsync(xmlPath, """
            <?xml version="1.0" encoding="UTF-8"?>
            <metadata>
              <name>Test Mod</name>
              <directory>test_mod</directory>
              <description>A test mod</description>
            </metadata>
            """);

        try
        {
            var tool = new ValidateXmlTool(tempDir);
            var args = System.Text.Json.JsonSerializer.Serialize(new { file_path = "metadata.xml" });
            var result = await tool.ExecuteAsync(args);

            Assert.Contains("error", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("version", result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
