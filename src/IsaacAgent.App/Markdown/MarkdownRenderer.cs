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

    private static readonly SolidColorBrush CodeColor = new(Color.Parse("#CE9178"));
    private static readonly SolidColorBrush QuoteColor = new(Color.Parse("#808080"));
    private static readonly SolidColorBrush LinkColor = new(Color.Parse("#569CD6"));
    private static readonly SolidColorBrush HrBrush = new(Color.Parse("#404040"));
    private static readonly FontFamily MonoFont = FontFamily.Parse("Cascadia Code,Consolas,monospace");

    private static void OnMarkdownChanged(Avalonia.Controls.SelectableTextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
    {
        var markdown = e.NewValue as string ?? "";
        textBlock.Inlines = ParseMarkdown(markdown);
    }

    private static InlineCollection ParseMarkdown(string markdown)
    {
        var inlines = new InlineCollection();
        var lines = markdown.Split('\n');
        var inCodeBlock = false;
        var codeBlockContent = new System.Text.StringBuilder();
        var codeBlockLang = "";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Code block fences
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    AddCodeBlock(inlines, codeBlockContent.ToString(), codeBlockLang);
                    codeBlockContent.Clear();
                    codeBlockLang = "";
                    inCodeBlock = false;
                    inlines.Add(new Run("\n"));
                }
                else
                {
                    inCodeBlock = true;
                    codeBlockLang = line.TrimStart()[3..].Trim();
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

            // Horizontal rule (--- , ***, ___ on a line by itself)
            if (IsHorizontalRule(line))
            {
                inlines.Add(new Run("\n"));
                inlines.Add(new Run(new string('\u2014', 40))
                {
                    Foreground = HrBrush,
                    FontSize = 12,
                });
                inlines.Add(new Run("\n"));
                continue;
            }

            // Headers
            if (line.StartsWith("#### "))
            {
                AddHeader(inlines, line[5..], 12);
                continue;
            }
            if (line.StartsWith("### "))
            {
                AddHeader(inlines, line[4..], 13);
                continue;
            }
            if (line.StartsWith("## "))
            {
                AddHeader(inlines, line[3..], 14);
                continue;
            }
            if (line.StartsWith("# "))
            {
                AddHeader(inlines, line[2..], 15);
                continue;
            }

            // Blockquote
            if (line.StartsWith("> "))
            {
                var quoteText = line[2..];
                ParseInlineFormatting(inlines, quoteText);
                StyleLastRun(inlines, r =>
                {
                    r.FontStyle = FontStyle.Italic;
                    r.Foreground = QuoteColor;
                });
                inlines.Add(new Run("\n"));
                continue;
            }

            // Unordered list (- / * / + followed by space)
            if (IsUnorderedListItem(line, out var ulIndent))
            {
                inlines.Add(new Run(new string(' ', ulIndent * 2) + "\u2022 "));
                ParseInlineFormatting(inlines, line[(ulIndent + 2)..]);
                inlines.Add(new Run("\n"));
                continue;
            }

            // Ordered list (1. / 2. etc.)
            if (IsOrderedListItem(line, out var olNumber, out var olIndent))
            {
                var numStr = olNumber.ToString();
                inlines.Add(new Run(new string(' ', olIndent * 2) + $"{numStr}. "));
                ParseInlineFormatting(inlines, line[(olIndent + numStr.Length + 2)..]);
                inlines.Add(new Run("\n"));
                continue;
            }

            // Regular paragraph line
            ParseInlineFormatting(inlines, line);
            if (i < lines.Length - 1)
                inlines.Add(new Run("\n"));
        }

        // Unterminated code block
        if (inCodeBlock && codeBlockContent.Length > 0)
            AddCodeBlock(inlines, codeBlockContent.ToString(), codeBlockLang);

        return inlines;
    }

    private static void AddHeader(InlineCollection inlines, string text, double fontSize)
    {
        ParseInlineFormatting(inlines, text);
        StyleLastRun(inlines, r =>
        {
            r.FontSize = fontSize;
            r.FontWeight = FontWeight.Bold;
        });
        inlines.Add(new Run("\n"));
    }

    private static void AddCodeBlock(InlineCollection inlines, string code, string lang)
    {
        if (!string.IsNullOrEmpty(lang))
        {
            inlines.Add(new Run($"{lang}\n")
            {
                FontFamily = MonoFont,
                FontSize = 11,
                Foreground = QuoteColor,
                FontStyle = FontStyle.Italic,
            });
        }
        inlines.Add(new Run(code)
        {
            FontFamily = MonoFont,
            FontSize = 12,
            Foreground = CodeColor,
        });
    }

    private static void StyleLastRun(InlineCollection inlines, Action<Run> styler)
    {
        if (inlines.Count > 0 && inlines[^1] is Run run)
            styler(run);
    }

    private static bool IsHorizontalRule(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3) return false;
        var c = trimmed[0];
        if (c != '-' && c != '*' && c != '_') return false;
        return trimmed.All(ch => ch == c);
    }

    private static bool IsUnorderedListItem(string line, out int indent)
    {
        indent = 0;
        while (indent < line.Length && line[indent] == ' ')
            indent++;
        if (indent + 1 >= line.Length) return false;
        var marker = line[indent];
        if (marker != '-' && marker != '*' && marker != '+') return false;
        return indent + 1 < line.Length && line[indent + 1] == ' ';
    }

    private static bool IsOrderedListItem(string line, out int number, out int indent)
    {
        number = 0;
        indent = 0;
        while (indent < line.Length && line[indent] == ' ')
            indent++;
        var start = indent;
        while (indent < line.Length && char.IsDigit(line[indent]))
        {
            number = number * 10 + (line[indent] - '0');
            indent++;
        }
        if (indent == start) return false;
        if (indent >= line.Length || line[indent] != '.') return false;
        indent++;
        if (indent >= line.Length || line[indent] != ' ') return false;
        return true;
    }

    private static readonly Regex InlineFormatRegex = new(
        @"(\*\*(.+?)\*\*)|(`(.+?)`)|(\*(.+?)\*)|(\[([^\]]+)\]\(([^)]+)\))",
        RegexOptions.Compiled);

    private static void ParseInlineFormatting(InlineCollection inlines, string text)
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
                    FontFamily = MonoFont,
                    Foreground = CodeColor,
                });
            }
            else if (match.Groups[6].Success) // *italic*
            {
                inlines.Add(new Run(match.Groups[6].Value) { FontStyle = FontStyle.Italic });
            }
            else if (match.Groups[8].Success) // [text](url)
            {
                inlines.Add(new Run(match.Groups[8].Value)
                {
                    Foreground = LinkColor,
                    Underline = true,
                });
            }

            pos = match.Index + match.Length;
        }

        if (pos < text.Length)
            inlines.Add(new Run(text[pos..]));
    }
}
