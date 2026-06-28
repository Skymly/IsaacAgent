using IsaacAgent.Rag.Chunking;
using Xunit;

namespace IsaacAgent.Tests;

public class SmartMarkdownChunkerTests
{
    [Fact]
    public void ChunkMarkdown_EmptyContent_ReturnsEmpty()
    {
        var chunks = SmartMarkdownChunker.ChunkMarkdown("", "test.md", "example");
        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkMarkdown_WhitespaceOnly_ReturnsEmpty()
    {
        var chunks = SmartMarkdownChunker.ChunkMarkdown("   \n\n   ", "test.md", "example");
        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkMarkdown_SimpleContent_ReturnsSingleChunk()
    {
        var md = """
            # Title

            This is a simple paragraph with enough text to be a valid chunk.
            It has multiple lines of content for testing.
            """;

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "test.md", "example");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("example", c.Source));
    }

    [Fact]
    public void ChunkMarkdown_WithFrontMatter_ParsesMetadata()
    {
        var md = """
            ---
            title: Custom Item Guide
            category: tutorial
            ---
            # Custom Item Guide

            This is the content of the guide with enough text.
            """;

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "guide.md", "example");

        Assert.NotEmpty(chunks);
        Assert.Contains("Custom Item Guide", chunks[0].Title);
        Assert.Equal("tutorial", chunks[0].Category);
    }

    [Fact]
    public void ChunkMarkdown_FrontMatterWithoutTitle_UsesFileName()
    {
        var md = """
            ---
            category: pattern
            ---
            Some content here that is long enough to be a valid chunk.
            """;

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "mypattern.md", "example");

        Assert.NotEmpty(chunks);
        Assert.Contains("mypattern", chunks[0].Title);
    }

    [Fact]
    public void ChunkMarkdown_MultipleHeadings_CreatesMultipleChunks()
    {
        var md = """
            # Main Title

            ## Section One
            This is the first section with enough text to be a valid chunk on its own.
            It has multiple lines to ensure it meets the minimum size requirement.

            ## Section Two
            This is the second section with enough text to be a valid chunk on its own.
            It also has multiple lines to ensure it meets the minimum size requirement.

            ## Section Three
            This is the third section with enough text to be a valid chunk on its own.
            It also has multiple lines to ensure it meets the minimum size requirement.
            """;

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "multi.md", "example");

        Assert.True(chunks.Count >= 2);
        Assert.Contains(chunks, c => c.Title.Contains("Section One"));
        Assert.Contains(chunks, c => c.Title.Contains("Section Two"));
        Assert.Contains(chunks, c => c.Title.Contains("Section Three"));
    }

    [Fact]
    public void ChunkMarkdown_CodeBlockNotSplit_KeepsCodeTogether()
    {
        var md = """
            # Code Example

            ```lua
            local mod = RegisterMod("TestMod", 1)
            mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
                print("hello world")
            end)
            ```
            """;

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "code.md", "example");

        Assert.NotEmpty(chunks);
        // The code block should be in a single chunk, not split
        var content = string.Join('\n', chunks.Select(c => c.Content));
        Assert.Contains("```lua", content);
        Assert.Contains("RegisterMod", content);
        Assert.Contains("```", content);
    }

    [Fact]
    public void ChunkMarkdown_HeadingInsideCodeBlock_NotTreatedAsHeading()
    {
        var md = """
            # Main

            ```markdown
            ## Not A Real Heading
            This is inside a code block.
            ```

            ## Real Heading
            This is outside the code block with enough text for a valid chunk.
            Multiple lines to ensure minimum size is met.
            """;

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "test.md", "example");

        // The "## Not A Real Heading" inside the code block should not create a new chunk
        Assert.DoesNotContain(chunks, c => c.Title.Contains("Not A Real Heading"));
        Assert.Contains(chunks, c => c.Title.Contains("Real Heading"));
    }

    [Fact]
    public void ChunkMarkdown_LargeSection_SplitsWithOverlap()
    {
        var longLine = new string('A', 2500);
        var md = $"# Big Section\n\n{longLine}";

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "big.md", "example");

        Assert.True(chunks.Count >= 2);
        // Check that chunks have part numbers in title
        Assert.Contains(chunks, c => c.Title.Contains("part"));
    }

    [Fact]
    public void ChunkMarkdown_SmallSectionsMerged_WithNeighbors()
    {
        var md = """
            # Title

            ## Tiny
            x

            ## Big Section
            This is a larger section with enough text to be a valid chunk on its own.
            It has multiple lines to ensure it meets the minimum size requirement.
            """;

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "merge.md", "example");

        // The tiny section should be merged with a neighbor, not emitted alone
        Assert.DoesNotContain(chunks, c => c.Title.EndsWith("— Tiny"));
    }

    [Fact]
    public void ChunkMarkdown_MetadataIncludesFileName()
    {
        var md = "Content with enough text to be a valid chunk for testing metadata.";

        var chunks = SmartMarkdownChunker.ChunkMarkdown(md, "mytest.md", "example");

        Assert.NotEmpty(chunks);
        Assert.Equal("mytest.md", chunks[0].Metadata["file"]);
    }

    [Fact]
    public void ChunkDirectory_NonExistentDir_ReturnsEmpty()
    {
        var chunks = SmartMarkdownChunker.ChunkDirectory("/nonexistent/path", "example");
        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkDirectory_ValidDir_LoadsMarkdownFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"isaac_chunk_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "doc1.md"), "# Doc 1\n\nContent for doc 1 with enough text.");
            File.WriteAllText(Path.Combine(tempDir, "doc2.md"), "# Doc 2\n\nContent for doc 2 with enough text.");

            var chunks = SmartMarkdownChunker.ChunkDirectory(tempDir, "test");

            Assert.NotEmpty(chunks);
            Assert.Contains(chunks, c => c.Content.Contains("Doc 1"));
            Assert.Contains(chunks, c => c.Content.Contains("Doc 2"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}

public class ApiDocChunkerTests
{
    [Fact]
    public void ChunkFromKnowledge_ReturnsNonEmpty()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public void ChunkFromKnowledge_ContainsCallbackChunks()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();

        Assert.Contains(chunks, c => c.Category == "callback");
        Assert.Contains(chunks, c => c.Id.StartsWith("callback:"));
    }

    [Fact]
    public void ChunkFromKnowledge_ContainsClassChunks()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();

        Assert.Contains(chunks, c => c.Category == "class");
        Assert.Contains(chunks, c => c.Id.StartsWith("class:"));
    }

    [Fact]
    public void ChunkFromKnowledge_ContainsEnumChunks()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();

        Assert.Contains(chunks, c => c.Category == "enum");
        Assert.Contains(chunks, c => c.Id.StartsWith("enum:"));
    }

    [Fact]
    public void ChunkFromKnowledge_CallbackChunks_ContainIdMetadata()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();
        var callbackChunks = chunks.Where(c => c.Category == "callback").ToList();

        Assert.NotEmpty(callbackChunks);
        Assert.All(callbackChunks, c => Assert.True(c.Metadata.ContainsKey("id")));
    }

    [Fact]
    public void ChunkFromKnowledge_ClassChunks_ContainCategoryMetadata()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();
        var classChunks = chunks.Where(c => c.Category == "class").ToList();

        Assert.NotEmpty(classChunks);
        Assert.All(classChunks, c => Assert.True(c.Metadata.ContainsKey("category")));
    }

    [Fact]
    public void ChunkFromKnowledge_EnumChunks_ContainValueCount()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();
        var enumChunks = chunks.Where(c => c.Category == "enum").ToList();

        Assert.NotEmpty(enumChunks);
        Assert.All(enumChunks, c => Assert.True(c.Metadata.ContainsKey("valueCount")));
    }

    [Fact]
    public void ChunkFromKnowledge_CallbackContent_ContainsArgsAndDescription()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();
        var callbackChunk = chunks.First(c => c.Category == "callback");

        Assert.Contains("Callback:", callbackChunk.Content);
        Assert.Contains("Arguments:", callbackChunk.Content);
        Assert.Contains("Description:", callbackChunk.Content);
    }

    [Fact]
    public void ChunkFromKnowledge_AllChunksHaveNonEmptyId()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();

        Assert.All(chunks, c => Assert.False(string.IsNullOrEmpty(c.Id)));
    }

    [Fact]
    public void ChunkFromKnowledge_AllChunksHaveSourceVanilla()
    {
        var chunks = ApiDocChunker.ChunkFromKnowledge();

        // Most chunks should be vanilla source
        Assert.Contains(chunks, c => c.Source == "vanilla");
    }
}

public class ChunkerHelpersTests
{
    [Fact]
    public void ParseFrontMatter_SimpleKeyValue_ParsesCorrectly()
    {
        var metadata = new Dictionary<string, string>();
        ChunkerHelpers.ParseFrontMatter("title: My Title\ncategory: tutorial", metadata);

        Assert.Equal("My Title", metadata["title"]);
        Assert.Equal("tutorial", metadata["category"]);
    }

    [Fact]
    public void ParseFrontMatter_MultiLineList_ParsesFirstItem()
    {
        var metadata = new Dictionary<string, string>();
        ChunkerHelpers.ParseFrontMatter("tags:\n  - Enum", metadata);

        Assert.Equal("Enum", metadata["tags"]);
    }

    [Fact]
    public void ParseFrontMatter_EmptyValue_SkipsKey()
    {
        var metadata = new Dictionary<string, string>();
        ChunkerHelpers.ParseFrontMatter("empty:", metadata);

        Assert.False(metadata.ContainsKey("empty"));
    }

    [Fact]
    public void ParseFrontMatter_EmptyString_ReturnsEmpty()
    {
        var metadata = new Dictionary<string, string>();
        ChunkerHelpers.ParseFrontMatter("", metadata);

        Assert.Empty(metadata);
    }

    [Fact]
    public void ParseFrontMatter_LineWithoutColon_Skipped()
    {
        var metadata = new Dictionary<string, string>();
        ChunkerHelpers.ParseFrontMatter("no colon here\ntitle: Test", metadata);

        Assert.False(metadata.ContainsKey("no colon here"));
        Assert.Equal("Test", metadata["title"]);
    }

    [Fact]
    public void ParseFrontMatter_MultipleKeys_AllParsed()
    {
        var metadata = new Dictionary<string, string>();
        ChunkerHelpers.ParseFrontMatter("title: Test\ncategory: pattern\nauthor: someone", metadata);

        Assert.Equal(3, metadata.Count);
        Assert.Equal("Test", metadata["title"]);
        Assert.Equal("pattern", metadata["category"]);
        Assert.Equal("someone", metadata["author"]);
    }

    [Fact]
    public void ParseFrontMatter_ValueWithColon_ParsesCorrectly()
    {
        var metadata = new Dictionary<string, string>();
        // The first colon is the separator, the rest is the value
        ChunkerHelpers.ParseFrontMatter("url: https://example.com", metadata);

        Assert.Equal("https://example.com", metadata["url"]);
    }
}
