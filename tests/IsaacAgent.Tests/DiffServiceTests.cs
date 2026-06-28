using IsaacAgent.App.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class DiffServiceTests
{
    private static DiffService CreateService() =>
        new(Mock.Of<ILogger<DiffService>>());

    [Fact]
    public void ParseDiff_EmptyOutput_NoFiles()
    {
        var svc = CreateService();
        svc.ParseDiff("");
        Assert.Empty(svc.Files);
    }

    [Fact]
    public void ParseDiff_SingleFileAdd_ParsesCorrectly()
    {
        var diff = """
            diff --git a/main.lua b/main.lua
            new file mode 100644
            index 0000000..abc123
            --- /dev/null
            +++ b/main.lua
            @@ -0,0 +1,3 @@
            +local mod = RegisterMod("Test", 1)
            +mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
            +end)
            """;

        var svc = CreateService();
        svc.ParseDiff(diff);

        Assert.Single(svc.Files);
        var file = svc.Files[0];
        Assert.Equal("main.lua", file.FilePath);
        Assert.True(file.IsNew);
        Assert.False(file.IsDeleted);
        Assert.Equal(3, file.AddedCount);
        Assert.Equal(0, file.RemovedCount);
    }

    [Fact]
    public void ParseDiff_FileModification_ParsesAddedAndRemoved()
    {
        var diff = """
            diff --git a/main.lua b/main.lua
            index abc123..def456 100644
            --- a/main.lua
            +++ b/main.lua
            @@ -1,3 +1,4 @@
             local mod = RegisterMod("Test", 1)
            -mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function() end)
            +mod:AddCallback(ModCallbacks.MC_POST_UPDATE, function()
            +    print("updated")
            +end)
             local unused = 1
            """;

        var svc = CreateService();
        svc.ParseDiff(diff);

        Assert.Single(svc.Files);
        var file = svc.Files[0];
        Assert.False(file.IsNew);
        Assert.False(file.IsDeleted);
        Assert.Equal(3, file.AddedCount);
        Assert.Equal(1, file.RemovedCount);
    }

    [Fact]
    public void ParseDiff_FileDeletion_SetsIsDeleted()
    {
        var diff = """
            diff --git a/old.lua b/old.lua
            deleted file mode 100644
            index abc123..0000000
            --- a/old.lua
            +++ /dev/null
            @@ -1,2 +0,0 @@
            -local old = 1
            -local unused = 2
            """;

        var svc = CreateService();
        svc.ParseDiff(diff);

        Assert.Single(svc.Files);
        var file = svc.Files[0];
        Assert.True(file.IsDeleted);
        Assert.Equal(0, file.AddedCount);
        Assert.Equal(2, file.RemovedCount);
    }

    [Fact]
    public void ParseDiff_MultipleFiles_AllParsed()
    {
        var diff = """
            diff --git a/file1.lua b/file1.lua
            index abc..def 100644
            --- a/file1.lua
            +++ b/file1.lua
            @@ -1 +1 @@
            -old
            +new
            diff --git a/file2.lua b/file2.lua
            new file mode 100644
            --- /dev/null
            +++ b/file2.lua
            @@ -0,0 +1 @@
            +content
            """;

        var svc = CreateService();
        svc.ParseDiff(diff);

        Assert.Equal(2, svc.Files.Count);
        Assert.Equal("file1.lua", svc.Files[0].FilePath);
        Assert.Equal("file2.lua", svc.Files[1].FilePath);
    }

    [Fact]
    public void ParseDiff_HunkHeader_SetsLineNumbers()
    {
        var diff = """
            diff --git a/test.lua b/test.lua
            --- a/test.lua
            +++ b/test.lua
            @@ -10,2 +10,2 @@
             context
            -removed
            +added
            """;

        var svc = CreateService();
        svc.ParseDiff(diff);

        var file = svc.Files[0];
        var removed = file.Lines.First(l => l.Type == DiffLine.LineType.Removed);
        var added = file.Lines.First(l => l.Type == DiffLine.LineType.Added);
        var context = file.Lines.First(l => l.Type == DiffLine.LineType.Context);

        Assert.Equal(10, context.OldLineNumber);
        Assert.Equal(10, context.NewLineNumber);
        Assert.Equal(11, removed.OldLineNumber);
        Assert.Equal(11, added.NewLineNumber);
    }

    [Fact]
    public void ParseDiff_NewFileHunk_StartsAtLine1()
    {
        var diff = """
            diff --git a/new.lua b/new.lua
            new file mode 100644
            --- /dev/null
            +++ b/new.lua
            @@ -0,0 +1,2 @@
            +first line
            +second line
            """;

        var svc = CreateService();
        svc.ParseDiff(diff);

        var file = svc.Files[0];
        var added = file.Lines.Where(l => l.Type == DiffLine.LineType.Added).ToList();
        Assert.Equal(2, added.Count);
        Assert.Equal(1, added[0].NewLineNumber);
        Assert.Equal(2, added[1].NewLineNumber);
    }

    [Fact]
    public void ParseDiff_CodeBlockLines_NotTreatedAsAdded()
    {
        // Lines starting with "+++" should be treated as file header, not added
        var diff = """
            diff --git a/test.lua b/test.lua
            --- a/test.lua
            +++ b/test.lua
            @@ -1,3 +1,3 @@
             local x = 1
            -local y = 2
            +local y = 3
             local z = 4
            """;

        var svc = CreateService();
        svc.ParseDiff(diff);

        var file = svc.Files[0];
        // The +++ line should not be counted as an added line
        Assert.Equal(1, file.AddedCount);
        Assert.DoesNotContain(file.Lines, l => l.Type == DiffLine.LineType.Added && l.Content.Contains("+++"));
    }

    [Fact]
    public void DiffLine_TypeLabel_ReturnsCorrectSymbol()
    {
        Assert.Equal("+", new DiffLine { Type = DiffLine.LineType.Added }.TypeLabel);
        Assert.Equal("-", new DiffLine { Type = DiffLine.LineType.Removed }.TypeLabel);
        Assert.Equal("@", new DiffLine { Type = DiffLine.LineType.Header }.TypeLabel);
        Assert.Equal(" ", new DiffLine { Type = DiffLine.LineType.Context }.TypeLabel);
    }

    [Fact]
    public void DiffFile_Summary_ContainsFilePathAndCounts()
    {
        var file = new DiffFile
        {
            FilePath = "main.lua",
            Lines =
            [
                new DiffLine { Type = DiffLine.LineType.Added, Content = "new" },
                new DiffLine { Type = DiffLine.LineType.Added, Content = "code" },
                new DiffLine { Type = DiffLine.LineType.Removed, Content = "old" }
            ]
        };

        Assert.Equal("main.lua (+2 -1)", file.Summary);
    }

    [Fact]
    public void DiffFile_DefaultValues_AreEmpty()
    {
        var file = new DiffFile();
        Assert.Equal("", file.FilePath);
        Assert.Equal("", file.OldPath);
        Assert.False(file.IsNew);
        Assert.False(file.IsDeleted);
        Assert.Empty(file.Lines);
        Assert.Equal(0, file.AddedCount);
        Assert.Equal(0, file.RemovedCount);
    }
}
