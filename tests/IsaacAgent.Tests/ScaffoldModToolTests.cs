using IsaacAgent.Tools.Implementations;
using Xunit;

namespace IsaacAgent.Tests;

public class ScaffoldModToolTests
{
    [Fact]
    public async Task ScaffoldMod_EscapesXmlSpecialChars_InMetadata()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_scaffold_xml_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new ScaffoldModTool(tempDir);
            // JSON: name = A<B>&C, description = D&E<F, author = G"H&I
            var args = """{"name":"A<B>&C","description":"D&E<F","author":"G\"H&I"}""";

            await tool.ExecuteAsync(args);

            var metadata = await File.ReadAllTextAsync(Path.Combine(tempDir, "metadata.xml"));
            Assert.Contains("<name>A&lt;B&gt;&amp;C</name>", metadata);
            Assert.Contains("<description>D&amp;E&lt;F</description>", metadata);
            Assert.Contains("<author>G&quot;H&amp;I</author>", metadata);

            // Should NOT contain raw unescaped special chars in element text
            Assert.DoesNotContain("<name>A<B>&C</name>", metadata);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldMod_EscapesLuaDoubleQuote_InMainLua()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_scaffold_luaq_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new ScaffoldModTool(tempDir);
            // JSON: name = Test"Quote  (\" in JSON is an escaped double quote)
            var args = """{"name":"Test\"Quote"}""";

            await tool.ExecuteAsync(args);

            var mainLua = await File.ReadAllTextAsync(Path.Combine(tempDir, "main.lua"));
            // The Lua string literal should have escaped the double quote as \"
            Assert.Contains("""RegisterMod("Test\"Quote", 1)""", mainLua);
            // Should NOT contain the unescaped quote that would break the Lua string
            Assert.DoesNotContain("""RegisterMod("Test"Quote", 1)""", mainLua);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldMod_EscapesLuaBackslash_InMainLua()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_scaffold_luab_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new ScaffoldModTool(tempDir);
            // JSON: name = Test\Name  (\\ in JSON is an escaped backslash)
            var args = """{"name":"Test\\Name"}""";

            await tool.ExecuteAsync(args);

            var mainLua = await File.ReadAllTextAsync(Path.Combine(tempDir, "main.lua"));
            // The Lua string literal should have escaped the backslash as \\
            Assert.Contains("""RegisterMod("Test\\Name", 1)""", mainLua);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldMod_PlainName_GeneratesValidFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_scaffold_plain_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new ScaffoldModTool(tempDir);
            var args = """{"name":"MyMod","description":"A cool mod","author":"Me","include_items":true}""";

            var result = await tool.ExecuteAsync(args);

            Assert.Contains("MyMod", result);
            Assert.True(File.Exists(Path.Combine(tempDir, "main.lua")));
            Assert.True(File.Exists(Path.Combine(tempDir, "metadata.xml")));
            Assert.True(File.Exists(Path.Combine(tempDir, "items.xml")));
            Assert.True(Directory.Exists(Path.Combine(tempDir, "resources", "gfx")));

            var metadata = await File.ReadAllTextAsync(Path.Combine(tempDir, "metadata.xml"));
            Assert.Contains("<name>MyMod</name>", metadata);
            Assert.Contains("<description>A cool mod</description>", metadata);

            var mainLua = await File.ReadAllTextAsync(Path.Combine(tempDir, "main.lua"));
            Assert.Contains("""RegisterMod("MyMod", 1)""", mainLua);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
