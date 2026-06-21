using IsaacAgent.Rag.Chunking;
using Xunit;
using System.Reflection;

namespace IsaacAgent.Tests;

public class MkDocsChunkerTests
{
    [Fact]
    public void ChunkMarkdown_ParsesTagsFrontMatter()
    {
        var md = """
            ---
            tags:
              - Enum
            search:
              boost: 3
            ---
            # Enum "ModCallbacks"

            ### MC_POST_UPDATE {: .copyable }
            Called after every game update.

            |DLC|Value|Name|
            |:--|:--|:--|
            |[ ](#){: .alldlc }|1|MC_POST_UPDATE {: .copyable }|

            ### MC_POST_RENDER {: .copyable }
            Called after every render.
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "enums/ModCallbacks.md", "vanilla");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("vanilla", c.Source));
        Assert.All(chunks, c => Assert.Equal("enum", c.Category));
        Assert.All(chunks, c => Assert.DoesNotContain("{: .copyable }", c.Content));
    }

    [Fact]
    public void ChunkMarkdown_ClassTag_MapsToClassCategory()
    {
        var md = """
            ---
            tags:
              - Class
            ---
            # Class "EntityPlayer"

            ### AddCacheFlags () {: aria-label='Functions' }
            #### void AddCacheFlags ( CacheFlag flags ) {: .copyable }
            Adds cache flags.

            ### AddBoneOrbital () {: aria-label='Functions' }
            #### void AddBoneOrbital ( ) {: .copyable }
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "EntityPlayer.md", "vanilla");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("class", c.Category));
        Assert.Contains(chunks, c => c.Title.Contains("AddCacheFlags"));
        Assert.Contains(chunks, c => c.Title.Contains("AddBoneOrbital"));
    }

    [Fact]
    public void ChunkMarkdown_FileTag_MapsToXmlCategory()
    {
        var md = """
            ---
            tags:
              - File
            ---
            # File "items.xml"

            | Variable Name | Description |
            |:--|:--|
            | id | int |
            | name | string |
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "xml/items.md", "vanilla");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("xml", c.Category));
    }

    [Fact]
    public void ChunkMarkdown_TutorialTag_MapsToTutorialCategory()
    {
        var md = """
            ---
            tags:
              - Tutorial
            ---
            # Creating a Custom Item

            ## Step 1
            Create main.lua.

            ## Step 2
            Register the callback.
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "tutorials/custom-item.md", "vanilla");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Equal("tutorial", c.Category));
    }

    [Fact]
    public void ChunkMarkdown_RepentogonSource_DistinguishedFromVanilla()
    {
        var md = """
            ---
            tags:
              - Class
            ---
            # Class "EntityPlayer"

            ### AddActiveCharge () {: aria-label='Functions' }
            #### int AddActiveCharge ( int Charge ) {: .copyable }
            Returns charge added.
            """;

        var vanillaChunks = MkDocsChunker.ChunkMarkdown(md, "EntityPlayer.md", "vanilla");
        var repentogonChunks = MkDocsChunker.ChunkMarkdown(md, "EntityPlayer.md", "repentogon");

        Assert.All(vanillaChunks, c => Assert.Equal("vanilla", c.Source));
        Assert.All(repentogonChunks, c => Assert.Equal("repentogon", c.Source));
    }

    [Fact]
    public void ChunkMarkdown_CleansAdmonitionSyntax()
    {
        var md = """
            ---
            tags:
              - Enum
            ---
            # Enum "Test"

            ### MC_TEST {: .copyable }
            Description here.

            ???- example "Example Code"
                ```lua
                print("hello")
                ```

            ???- warning "Warning"
                Be careful.
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "Test.md", "vanilla");

        Assert.NotEmpty(chunks);
        var content = string.Join('\n', chunks.Select(c => c.Content));
        Assert.DoesNotContain("???-", content);
        Assert.Contains("Example Code", content);
        Assert.Contains("Warning", content);
    }

    [Fact]
    public void ChunkMarkdown_SplitsByH3Headings()
    {
        var md = """
            ---
            tags:
              - Enum
            ---
            # Enum "Callbacks"

            ### MC_FIRST {: .copyable }
            First callback description.

            ### MC_SECOND {: .copyable }
            Second callback description.

            ### MC_THIRD {: .copyable }
            Third callback description.
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "Callbacks.md", "vanilla");

        var h3Chunks = chunks.Where(c => c.Title.Contains("MC_")).ToList();
        Assert.True(h3Chunks.Count >= 3);
        Assert.Contains(h3Chunks, c => c.Title.Contains("MC_FIRST"));
        Assert.Contains(h3Chunks, c => c.Title.Contains("MC_SECOND"));
        Assert.Contains(h3Chunks, c => c.Title.Contains("MC_THIRD"));
    }

    [Fact]
    public void ChunkMarkdown_ExtractsDocTitleFromH1()
    {
        var md = """
            ---
            tags:
              - Class
            ---
            # Class "Room"

            ### GetRoom() {: .copyable }
            Returns the room.
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "Room.md", "vanilla");

        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.Contains("Room", c.Title));
    }

    [Fact]
    public void ChunkMarkdown_OnlyHeading_ProducesSingleChunk()
    {
        var md = """
            ---
            tags:
              - Enum
            ---
            # Enum "Empty"
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "Empty.md", "vanilla");

        // A lone H1 heading still produces a chunk with the heading as content
        Assert.Single(chunks);
        Assert.Equal("enum", chunks[0].Category);
    }

    [Fact]
    public void ChunkMarkdown_PathBasedCategoryFallback()
    {
        var md = """
            # Some Doc

            Content without frontmatter.
            With enough text to not be too short for the single chunk path.
            Adding more text here to ensure it passes the length threshold.
            """;

        var chunks = MkDocsChunker.ChunkMarkdown(md, "enums/Something.md", "vanilla");

        Assert.NotEmpty(chunks);
        Assert.Equal("enum", chunks[0].Category);
    }

    [Fact]
    public void ChunkFromEmbeddedResources_LoadsAllVanillaDocs()
    {
        var asm = Assembly.GetExecutingAssembly();
        // Test assembly references IsaacAgent.Rag, so load it
        var ragAsm = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "IsaacAgent.Rag");

        var chunks = MkDocsChunker.ChunkFromEmbeddedResources(
            ragAsm, "IsaacAgent.Rag.Resources.docs.vanilla", "vanilla");

        Assert.NotEmpty(chunks);
        Assert.True(chunks.Count > 100, $"Expected >100 vanilla chunks, got {chunks.Count}");
        Assert.All(chunks, c => Assert.Equal("vanilla", c.Source));
        Assert.Contains(chunks, c => c.Category == "enum");
        Assert.Contains(chunks, c => c.Category == "class");
        Assert.Contains(chunks, c => c.Category == "xml");
    }

    [Fact]
    public void ChunkFromEmbeddedResources_LoadsAllRepentogonDocs()
    {
        var ragAsm = AppDomain.CurrentDomain.GetAssemblies()
            .First(a => a.GetName().Name == "IsaacAgent.Rag");

        var chunks = MkDocsChunker.ChunkFromEmbeddedResources(
            ragAsm, "IsaacAgent.Rag.Resources.docs.repentogon", "repentogon");

        Assert.NotEmpty(chunks);
        Assert.True(chunks.Count > 100, $"Expected >100 repentogon chunks, got {chunks.Count}");
        Assert.All(chunks, c => Assert.Equal("repentogon", c.Source));
    }
}
