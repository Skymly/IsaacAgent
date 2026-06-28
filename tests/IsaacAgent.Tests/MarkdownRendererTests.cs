using IsaacAgent.App.Markdown;
using Xunit;

namespace IsaacAgent.Tests;

[Collection("Avalonia")]
public class MarkdownRendererTests
{
    // ── Table rendering ────────────────────────────────────────

    [Fact]
    public void Table_SimpleTwoColumns_RendersAlignedRows()
    {
        var md = """
                 | Name | Value |
                 |------|-------|
                 | HP | 100 |
                 | Speed | 2.5 |
                 """;

        var text = MarkdownRenderer.RenderToText(md);

        // Header row
        Assert.Contains("Name", text);
        Assert.Contains("Value", text);
        // Separator line
        Assert.Contains("------", text);
        // Data rows
        Assert.Contains("HP", text);
        Assert.Contains("100", text);
        Assert.Contains("Speed", text);
        Assert.Contains("2.5", text);
    }

    [Fact]
    public void Table_ColumnsArePaddedToEqualWidth()
    {
        var md = """
                 | A | B |
                 |---|---|
                 | x | y |
                 """;

        var text = MarkdownRenderer.RenderToText(md);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Header and separator and data row should all be present
        Assert.True(lines.Length >= 3);
        // Header line contains "A" and "B"
        Assert.Contains("A", lines[0]);
        Assert.Contains("B", lines[0]);
        // Separator contains dashes
        Assert.Contains("---", lines[1]);
        // Data row contains "x" and "y"
        Assert.Contains("x", lines[2]);
        Assert.Contains("y", lines[2]);
    }

    [Fact]
    public void Table_ThreeColumns_RendersAllColumns()
    {
        var md = """
                 | Callback | Type | Description |
                 |----------|------|-------------|
                 | MC_POST_UPDATE | void | Called every frame |
                 | MC_USE_ITEM | Entity | Called on item use |
                 """;

        var text = MarkdownRenderer.RenderToText(md);

        Assert.Contains("Callback", text);
        Assert.Contains("MC_POST_UPDATE", text);
        Assert.Contains("MC_USE_ITEM", text);
        Assert.Contains("Called every frame", text);
        Assert.Contains("Called on item use", text);
    }

    [Fact]
    public void Table_WithUnevenCells_HandlesMissingColumns()
    {
        var md = """
                 | A | B | C |
                 |---|---|---|
                 | only one |
                 """;

        var text = MarkdownRenderer.RenderToText(md);

        // Should not crash and should contain the cell text
        Assert.Contains("only one", text);
        Assert.Contains("A", text);
        Assert.Contains("B", text);
        Assert.Contains("C", text);
    }

    [Fact]
    public void Table_WithEscapedPipeInCell_PreservesPipe()
    {
        var md = """
                 | Command | Args |
                 |---------|-------|
                 | grep \| a \| b |
                 """;

        var text = MarkdownRenderer.RenderToText(md);

        // The escaped pipe should appear as a literal pipe in the cell
        Assert.Contains("grep | a | b", text);
    }

    [Fact]
    public void Table_SeparatorWithColons_IsRecognizedAsTable()
    {
        var md = """
                 | Left | Center | Right |
                 |:-----|:------:|------:|
                 | a | b | c |
                 """;

        var text = MarkdownRenderer.RenderToText(md);

        Assert.Contains("Left", text);
        Assert.Contains("Center", text);
        Assert.Contains("Right", text);
        Assert.Contains("a", text);
        Assert.Contains("b", text);
        Assert.Contains("c", text);
    }

    [Fact]
    public void Table_FollowedByParagraph_BothRendered()
    {
        var md = """
                 | Key | Value |
                 |-----|-------|
                 | HP | 100 |

                 This is a paragraph after the table.
                 """;

        var text = MarkdownRenderer.RenderToText(md);

        Assert.Contains("HP", text);
        Assert.Contains("100", text);
        Assert.Contains("This is a paragraph after the table.", text);
    }

    [Fact]
    public void Table_NoTrailingPipe_StillParsed()
    {
        var md = """
                 | Name | Value
                 |------|-------
                 | HP | 100
                 """;

        var text = MarkdownRenderer.RenderToText(md);

        Assert.Contains("Name", text);
        Assert.Contains("HP", text);
        Assert.Contains("100", text);
    }

    // ── Non-table edge cases ───────────────────────────────────

    [Fact]
    public void NonTable_PipeInText_NotTreatedAsTable()
    {
        var md = "This has a | pipe in it but is not a table.";

        var text = MarkdownRenderer.RenderToText(md);

        Assert.Contains("This has a", text);
        // Should not contain separator dashes
        Assert.DoesNotContain("------", text);
    }

    [Fact]
    public void Table_OnlyHeaderAndSeparator_NoDataRows_StillRenders()
    {
        var md = """
                 | Col1 | Col2 |
                 |------|-------|
                 """;

        var text = MarkdownRenderer.RenderToText(md);

        Assert.Contains("Col1", text);
        Assert.Contains("Col2", text);
        Assert.Contains("------", text);
    }

    // ── Existing markdown features (regression) ────────────────

    [Fact]
    public void Bold_RendersCorrectly()
    {
        var text = MarkdownRenderer.RenderToText("**bold text**");
        Assert.Contains("bold text", text);
    }

    [Fact]
    public void InlineCode_RendersCorrectly()
    {
        var text = MarkdownRenderer.RenderToText("Use `Isaac.GetPlayer()` to get the player.");
        Assert.Contains("Isaac.GetPlayer()", text);
    }

    [Fact]
    public void CodeBlock_RendersCorrectly()
    {
        var md = """
                 ```lua
                 function mod:OnUpdate()
                     print("hello")
                 end
                 ```
                 """;

        var text = MarkdownRenderer.RenderToText(md);
        Assert.Contains("function mod:OnUpdate()", text);
        Assert.Contains("print", text);
        Assert.Contains("lua", text); // language label
    }

    [Fact]
    public void Header_AllLevels_RenderCorrectly()
    {
        var text = MarkdownRenderer.RenderToText("# H1\n## H2\n### H3\n#### H4");
        Assert.Contains("H1", text);
        Assert.Contains("H2", text);
        Assert.Contains("H3", text);
        Assert.Contains("H4", text);
    }

    [Fact]
    public void UnorderedList_RendersWithBullet()
    {
        var md = """
                 - First item
                 - Second item
                 - Third item
                 """;

        var text = MarkdownRenderer.RenderToText(md);
        Assert.Contains("First item", text);
        Assert.Contains("Second item", text);
        Assert.Contains("Third item", text);
        Assert.Contains("\u2022", text); // bullet character
    }

    [Fact]
    public void OrderedList_RendersWithNumbers()
    {
        var md = """
                 1. First
                 2. Second
                 3. Third
                 """;

        var text = MarkdownRenderer.RenderToText(md);
        Assert.Contains("1.", text);
        Assert.Contains("2.", text);
        Assert.Contains("3.", text);
    }

    [Fact]
    public void Link_RendersAsText()
    {
        var text = MarkdownRenderer.RenderToText("[Isaac Docs](https:// IsaacDocs.com)");
        Assert.Contains("Isaac Docs", text);
    }

    [Fact]
    public void Blockquote_RendersAsText()
    {
        var text = MarkdownRenderer.RenderToText("> This is a quote");
        Assert.Contains("This is a quote", text);
    }

    [Fact]
    public void HorizontalRule_RendersAsDashes()
    {
        var text = MarkdownRenderer.RenderToText("---");
        Assert.Contains("\u2014", text); // em dash
    }

    [Fact]
    public void EmptyString_RendersWithoutError()
    {
        var text = MarkdownRenderer.RenderToText("");
        Assert.Equal("", text);
    }
}
