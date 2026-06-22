using IsaacAgent.Rag.Store;
using IsaacAgent.Rag.Tools;
using IsaacAgent.Tools.Implementations;
using Xunit;

namespace IsaacAgent.Tests;

public class PathTraversalTests
{
    [Fact]
    public async Task ValidateXmlTool_PathTraversal_ReturnsError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_xml_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new ValidateXmlTool(tempDir);
            var args = """{"file_path":"../../../etc/passwd"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Path traversal", result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ValidateXmlTool_SiblingDirectoryPrefix_ReturnsError()
    {
        // Create two directories with similar prefix: C:\test_proj and C:\test_proj_evil
        var baseDir = Path.Combine(Path.GetTempPath(), $"isaac_prefix_test_{Guid.NewGuid():N}");
        var safeDir = Path.Combine(baseDir, "myproject");
        var evilDir = Path.Combine(baseDir, "myproject_evil");
        Directory.CreateDirectory(safeDir);
        Directory.CreateDirectory(evilDir);
        try
        {
            // Place a file in the "evil" sibling directory
            var evilFile = Path.Combine(evilDir, "secret.xml");
            await File.WriteAllTextAsync(evilFile, "<root/>");

            var tool = new ValidateXmlTool(safeDir);
            // Try to access the sibling via relative path
            var args = """{"file_path":"../myproject_evil/secret.xml"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Path traversal", result);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task ValidateXmlTool_ValidRelativePath_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_xml_valid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var xmlFile = Path.Combine(tempDir, "metadata.xml");
            await File.WriteAllTextAsync(xmlFile, "<metadata/>");

            var tool = new ValidateXmlTool(tempDir);
            var args = """{"file_path":"metadata.xml"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.DoesNotContain("Path traversal", result);
            Assert.True(result.Contains("valid") || result.Contains("error") || result.Contains("Error"),
                $"Unexpected result: {result}");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ReadFileTool_SiblingDirectoryPrefix_ReturnsError()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"isaac_read_prefix_{Guid.NewGuid():N}");
        var safeDir = Path.Combine(baseDir, "myproject");
        var evilDir = Path.Combine(baseDir, "myproject_evil");
        Directory.CreateDirectory(safeDir);
        Directory.CreateDirectory(evilDir);
        try
        {
            var evilFile = Path.Combine(evilDir, "secret.lua");
            await File.WriteAllTextAsync(evilFile, "local x = 1");

            var tool = new ReadFileTool(safeDir);
            var args = """{"path":"../myproject_evil/secret.lua"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Path traversal", result);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task ReadFileTool_ValidRelativePath_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_read_valid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var file = Path.Combine(tempDir, "main.lua");
            await File.WriteAllTextAsync(file, "local mod = RegisterMod('Test', 1)");

            var tool = new ReadFileTool(tempDir);
            var args = """{"path":"main.lua"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.DoesNotContain("Path traversal", result);
            Assert.Contains("RegisterMod", result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task WriteFileTool_SiblingDirectoryPrefix_ReturnsError()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"isaac_write_prefix_{Guid.NewGuid():N}");
        var safeDir = Path.Combine(baseDir, "myproject");
        var evilDir = Path.Combine(baseDir, "myproject_evil");
        Directory.CreateDirectory(safeDir);
        Directory.CreateDirectory(evilDir);
        try
        {
            var tool = new WriteFileTool(safeDir);
            var args = """{"path":"../myproject_evil/evil.lua","content":"print('pwned')"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Path traversal", result);
            Assert.False(File.Exists(Path.Combine(evilDir, "evil.lua")));
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task WriteFileTool_ValidRelativePath_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_write_valid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var tool = new WriteFileTool(tempDir);
            var args = """{"path":"scripts/init.lua","content":"print('hi')"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.DoesNotContain("Path traversal", result);
            Assert.True(File.Exists(Path.Combine(tempDir, "scripts", "init.lua")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ListFilesTool_SiblingDirectoryPrefix_ReturnsError()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"isaac_list_prefix_{Guid.NewGuid():N}");
        var safeDir = Path.Combine(baseDir, "myproject");
        var evilDir = Path.Combine(baseDir, "myproject_evil");
        Directory.CreateDirectory(safeDir);
        Directory.CreateDirectory(evilDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(evilDir, "secret.lua"), "local x = 1");

            var tool = new ListFilesTool(safeDir);
            var args = """{"subdir":"../myproject_evil"}""";

            var result = await tool.ExecuteAsync(args);

            Assert.Contains("Path traversal", result);
        }
        finally
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public async Task ListFilesTool_ValidRelativePath_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_list_valid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "main.lua"), "");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "metadata.xml"), "");

            var tool = new ListFilesTool(tempDir);
            var args = """{}""";

            var result = await tool.ExecuteAsync(args);

            Assert.DoesNotContain("Path traversal", result);
            Assert.Contains("main.lua", result);
            Assert.Contains("metadata.xml", result);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}

public class InMemoryVectorStoreLoadTests
{
    [Fact]
    public async Task LoadAsync_NonExistentFile_ReturnsFalse()
    {
        var store = new InMemoryVectorStore();
        var result = await store.LoadAsync(Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.bin"));
        Assert.False(result);
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_ReturnsFalse()
    {
        // Write a valid header (correct version + model name) but claim 100 entries
        // with no actual data, so reading entries hits EndOfStreamException.
        var tempFile = Path.Combine(Path.GetTempPath(), $"corrupt_{Guid.NewGuid():N}.bin");
        await using (var fs = File.Create(tempFile))
        await using (var writer = new BinaryWriter(fs))
        {
            writer.Write(1u);            // IndexFormatVersion
            writer.Write("test-model");  // ModelName
            writer.Write(768);           // Dimensions
            writer.Write(0L);            // BuiltAt
            writer.Write(100);           // Entries.Count = 100 but no data follows
        }
        try
        {
            var store = new InMemoryVectorStore();
            var result = await store.LoadAsync(tempFile);
            Assert.False(result);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_IncompatibleVersion_ReturnsFalse()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"oldversion_{Guid.NewGuid():N}.bin");
        await using (var fs = File.Create(tempFile))
        await using (var writer = new BinaryWriter(fs))
        {
            writer.Write(999u);
            writer.Write("test");
            writer.Write(768);
            writer.Write(0L);
            writer.Write(0);
        }
        try
        {
            var store = new InMemoryVectorStore();
            var result = await store.LoadAsync(tempFile);
            Assert.False(result);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"roundtrip_{Guid.NewGuid():N}.bin");
        var store = new InMemoryVectorStore();

        store.ReplaceAll("test-model", 3, new[]
        {
            new VectorStoreEntry
            {
                Chunk = new IsaacAgent.Core.Models.KnowledgeChunk
                {
                    Id = "chunk1",
                    Source = "test",
                    Category = "api",
                    Title = "Test",
                    Content = "Test content",
                    Metadata = { ["key"] = "value" }
                },
                Vector = [1f, 0.5f, 0.25f]
            }
        });

        try
        {
            await store.SaveAsync(tempFile);

            var store2 = new InMemoryVectorStore();
            var loaded = await store2.LoadAsync(tempFile);

            Assert.True(loaded);
            Assert.Equal("test-model", store2.ModelName);
            Assert.Equal(3, store2.Dimensions);
            Assert.Equal(1, store2.Count);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
