using IsaacAgent.App.Services;
using IsaacAgent.Core.Models;
using Xunit;

namespace IsaacAgent.Tests;

public class ScaffoldingServiceTests
{
    [Fact]
    public async Task CreateSkeletonAsync_WritesMainLuaAndMetadata()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_scaffold_svc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var sut = new ScaffoldingService();

            await sut.CreateSkeletonAsync(tempDir, "MyMod", "A cool mod", "Me");

            Assert.True(File.Exists(Path.Combine(tempDir, "main.lua")));
            Assert.True(File.Exists(Path.Combine(tempDir, "metadata.xml")));
            Assert.True(Directory.Exists(Path.Combine(tempDir, "resources", "gfx")));

            var mainLua = await File.ReadAllTextAsync(Path.Combine(tempDir, "main.lua"));
            Assert.Contains("""RegisterMod("MyMod", 1)""", mainLua);

            var metadata = await File.ReadAllTextAsync(Path.Combine(tempDir, "metadata.xml"));
            Assert.Contains("<name>MyMod</name>", metadata);
            Assert.Contains("<description>A cool mod</description>", metadata);
            Assert.Contains("<author>Me</author>", metadata);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldFromTemplateAsync_CreatesFilesAndSubstitutesPlaceholders()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_scaffold_tmpl_{Guid.NewGuid():N}");
        try
        {
            var sut = new ScaffoldingService();
            var template = ModTemplates.All[0];

            var (files, error) = await sut.ScaffoldFromTemplateAsync(
                tempDir, template, "CustomMod", "My description", "MyAuthor");

            Assert.Null(error);
            Assert.NotNull(files);
            Assert.True(files.Length > 0);
            Assert.True(File.Exists(Path.Combine(tempDir, "main.lua")));

            var content = await File.ReadAllTextAsync(Path.Combine(tempDir, "main.lua"));
            Assert.Contains("CustomMod", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldFromTemplateAsync_EmptyName_UsesDefault()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_scaffold_def_{Guid.NewGuid():N}");
        try
        {
            var sut = new ScaffoldingService();

            var (files, error) = await sut.ScaffoldFromTemplateAsync(
                tempDir, ModTemplates.All[0], name: "  ");

            Assert.Null(error);
            Assert.NotNull(files);
            var content = await File.ReadAllTextAsync(Path.Combine(tempDir, "main.lua"));
            Assert.Contains("MyMod", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ScaffoldFromTemplateAsync_EscapesLuaStringInModName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_scaffold_esc_{Guid.NewGuid():N}");
        try
        {
            var sut = new ScaffoldingService();

            var (files, error) = await sut.ScaffoldFromTemplateAsync(
                tempDir, ModTemplates.All[0], name: "Test\"Mod\\Name");

            Assert.Null(error);
            var content = await File.ReadAllTextAsync(Path.Combine(tempDir, "main.lua"));
            Assert.Contains("Test\\\"Mod\\\\Name", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
