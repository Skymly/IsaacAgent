using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace IsaacAgent.App.Markdown;

public static class MarkdownRenderer
{
    public static readonly AttachedProperty<string> MarkdownProperty =
        AvaloniaProperty.RegisterAttached<Avalonia.Controls.SelectableTextBlock, string>(
            "Markdown", typeof(MarkdownRenderer), string.Empty);

    static MarkdownRenderer()
    {
        MarkdownProperty.Changed.AddClassHandler<Avalonia.Controls.SelectableTextBlock>(OnMarkdownChanged);
    }

    public static string GetMarkdown(Avalonia.Controls.SelectableTextBlock element) => element.GetValue(MarkdownProperty);
    public static void SetMarkdown(Avalonia.Controls.SelectableTextBlock element, string value) => element.SetValue(MarkdownProperty, value);

    private static void OnMarkdownChanged(Avalonia.Controls.SelectableTextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
    {
        var markdown = e.NewValue as string ?? "";
        textBlock.Inlines = ParseMarkdown(markdown, textBlock);
    }

    private static InlineCollection ParseMarkdown(string markdown, Avalonia.Controls.SelectableTextBlock textBlock)
    {
        var inlines = new InlineCollection();
        var lines = markdown.Split('\n');
        var inCodeBlock = false;
        var codeBlockContent = new System.Text.StringBuilder();

        var monoFontFamily = FontFamily.Parse("Cascadia Code,Consolas,monospace");

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    // End code block
                    inlines.Add(new Run(codeBlockContent.ToString())
                    {
                        FontFamily = monoFontFamily,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Color.Parse("#CE9178")),
                    });
                    codeBlockContent.Clear();
                    inCodeBlock = false;
                    inlines.Add(new Run("\n"));
                }
                else
                {
                    // Start code block
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockContent.Append(line);
                if (i < lines.Length - 1)
                    codeBlockContent.Append('\n');
                continue;
            }

            // Headers
            if (line.StartsWith("### "))
            {
                inlines.Add(new Run(line[4..] + "\n")
                {
                    FontSize = 13,
                    FontWeight = FontWeight.Bold,
                });
                continue;
            }
            if (line.StartsWith("## "))
            {
                inlines.Add(new Run(line[3..] + "\n")
                {
                    FontSize = 14,
                    FontWeight = FontWeight.Bold,
                });
                continue;
            }
            if (line.StartsWith("# "))
            {
                inlines.Add(new Run(line[2..] + "\n")
                {
                    FontSize = 15,
                    FontWeight = FontWeight.Bold,
                });
                continue;
            }

            // Parse inline formatting: **bold**, `code`, *italic*
            ParseInlineFormatting(inlines, line, monoFontFamily);
            if (i < lines.Length - 1)
                inlines.Add(new Run("\n"));
        }

        if (inCodeBlock && codeBlockContent.Length > 0)
        {
            inlines.Add(new Run(codeBlockContent.ToString())
            {
                FontFamily = monoFontFamily,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#CE9178")),
            });
        }

        return inlines;
    }

    private static readonly Regex InlineFormatRegex = new(
        @"(\*\*(.+?)\*\*)|(`(.+?)`)|(\*(.+?)\*)",
        RegexOptions.Compiled);

    private static void ParseInlineFormatting(InlineCollection inlines, string text, FontFamily monoFont)
    {
        var pos = 0;
        foreach (Match match in InlineFormatRegex.Matches(text))
        {
            if (match.Index > pos)
                inlines.Add(new Run(text[pos..match.Index]));

            if (match.Groups[2].Success) // **bold**
            {
                inlines.Add(new Run(match.Groups[2].Value) { FontWeight = FontWeight.Bold });
            }
            else if (match.Groups[4].Success) // `code`
            {
                inlines.Add(new Run(match.Groups[4].Value)
                {
                    FontFamily = monoFont,
                    Foreground = new SolidColorBrush(Color.Parse("#CE9178")),
                });
            }
            else if (match.Groups[6].Success) // *italic*
            {
                inlines.Add(new Run(match.Groups[6].Value) { FontStyle = FontStyle.Italic });
            }

            pos = match.Index + match.Length;
        }

        if (pos < text.Length)
            inlines.Add(new Run(text[pos..]));
    }
}
