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

    private static IBrush ResolveBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var v) == true && v is IBrush b)
            return b;
        return new SolidColorBrush(Colors.Transparent);
    }

    private static IBrush CodeBrush => ResolveBrush("IsaacCodeBrush");
    private static IBrush QuoteBrush => ResolveBrush("IsaacMarkdownQuoteBrush");
    private static IBrush LinkBrush => ResolveBrush("IsaacLinkBrush");
    private static IBrush HrColorBrush => ResolveBrush("IsaacMarkdownHrBrush");
    private static IBrush KeywordBrush => ResolveBrush("IsaacSyntaxKeywordBrush");
    private static IBrush StringBrush => ResolveBrush("IsaacSyntaxStringBrush");
    private static IBrush CommentBrush => ResolveBrush("IsaacSyntaxCommentBrush");
    private static IBrush NumberBrush => ResolveBrush("IsaacSyntaxNumberBrush");
    private static IBrush FunctionBrush => ResolveBrush("IsaacSyntaxFunctionBrush");
    private static readonly FontFamily MonoFont = FontFamily.Parse("Cascadia Code,Consolas,monospace");

    /// <summary>
    ///   Lua keywords for syntax highlighting.
    /// </summary>
    private static readonly HashSet<string> LuaKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "break", "do", "else", "elseif", "end", "false", "for",
        "function", "goto", "if", "in", "local", "nil", "not", "or",
        "repeat", "return", "then", "true", "until", "while"
    };

    /// <summary>
    ///   Common Isaac modding API globals for syntax highlighting.
    /// </summary>
    private static readonly HashSet<string> LuaGlobals = new(StringComparer.OrdinalIgnoreCase)
    {
        "Isaac", "Game", "ModCallbacks", "EntityType", "PickupVariant",
        "TearVariant", "FamiliarVariant", "EffectVariant", "EntityVariant",
        "NpcState", "RoomShape", "BedroomMode", "LevelStage",
        "CollectibleType", "TrinketType", "CardType", "PillEffect",
        "Vector", "Color", "Sprite", "SFX", "MusicManager",
        "Input", "Options", "Challenge", "Difficulty", "NullItemID"
    };

    private static void OnMarkdownChanged(Avalonia.Controls.SelectableTextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
    {
        var markdown = e.NewValue as string ?? "";
        textBlock.Inlines = ParseMarkdown(markdown);
    }

    /// <summary>
    ///   Extracts plain text from an InlineCollection for testing.
    ///   Concatenates all Run text content.
    /// </summary>
    internal static string RenderToText(string markdown)
    {
        var inlines = ParseMarkdown(markdown);
        var sb = new System.Text.StringBuilder();
        foreach (var inline in inlines)
        {
            if (inline is Run run)
                sb.Append(run.Text);
        }
        return sb.ToString();
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
                    Foreground = HrColorBrush,
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
                    r.Foreground = QuoteBrush;
                });
                inlines.Add(new Run("\n"));
                continue;
            }

            // Unordered list (- / * / + followed by space)
            if (IsUnorderedListItem(line, out var ulIndent))
            {
                var content = line[(ulIndent + 2)..];

                // Task list: - [ ] or - [x]
                if (content.StartsWith("[ ] ") || content.StartsWith("[ ]\t"))
                {
                    inlines.Add(new Run(new string(' ', ulIndent * 2) + "\u2610 ")); // ☐ ballot box
                    ParseInlineFormatting(inlines, content[4..]);
                    inlines.Add(new Run("\n"));
                    continue;
                }
                if (content.StartsWith("[x] ") || content.StartsWith("[X] ") ||
                    content.StartsWith("[x]\t") || content.StartsWith("[X]\t"))
                {
                    inlines.Add(new Run(new string(' ', ulIndent * 2) + "\u2612 ")); // ☒ ballot box with X
                    ParseInlineFormatting(inlines, content[4..]);
                    inlines.Add(new Run("\n"));
                    continue;
                }

                inlines.Add(new Run(new string(' ', ulIndent * 2) + "\u2022 "));
                ParseInlineFormatting(inlines, content);
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

            // Table (header row | separator row | data rows)
            if (IsTableRow(line) && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                var tableLines = new List<string> { line, lines[i + 1] };
                var dataStart = i + 2;
                while (dataStart < lines.Length && IsTableRow(lines[dataStart]))
                {
                    tableLines.Add(lines[dataStart]);
                    dataStart++;
                }
                AddTable(inlines, tableLines);
                i = dataStart - 1; // skip consumed lines
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
                Foreground = QuoteBrush,
                FontStyle = FontStyle.Italic,
            });
        }

        // Apply syntax highlighting for Lua code blocks
        if (string.IsNullOrEmpty(lang) ||
            lang.Equals("lua", StringComparison.OrdinalIgnoreCase))
        {
            AddHighlightedLuaCode(inlines, code);
        }
        else
        {
            inlines.Add(new Run(code)
            {
                FontFamily = MonoFont,
                FontSize = 12,
                Foreground = CodeBrush,
            });
        }
    }

    /// <summary>
    ///   Renders Lua code with syntax highlighting: keywords, strings,
    ///   comments, numbers, and function calls get distinct colors.
    /// </summary>
    private static void AddHighlightedLuaCode(InlineCollection inlines, string code)
    {
        // Tokenize: comments, strings, identifiers, numbers, and other
        var regex = new Regex(
            @"(?<comment>--\[\[[\s\S]*?\]\]|--[^\n]*)|" +
            @"\[\[(?<longstring>[\s\S]*?)\]\]|" +
            "(?<dstring>\"(\\\\.|[^\"\\\\])*\")|" +
            @"(?<sstring>'(\\.|[^'\\])*')|" +
            @"(?<number>\b\d+\.?\d*(?:[eE][+-]?\d+)?\b)|" +
            @"(?<ident>[A-Za-z_]\w*)" +
            @"|(?<whitespace>\s+)|" +
            @"(?<other>.)",
            RegexOptions.Compiled);

        foreach (Match match in regex.Matches(code))
        {
            if (match.Groups["comment"].Success)
            {
                inlines.Add(new Run(match.Groups["comment"].Value)
                {
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Foreground = CommentBrush,
                    FontStyle = FontStyle.Italic,
                });
            }
            else if (match.Groups["longstring"].Success)
            {
                inlines.Add(new Run(match.Groups["longstring"].Value)
                {
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Foreground = StringBrush,
                });
            }
            else if (match.Groups["dstring"].Success)
            {
                inlines.Add(new Run(match.Groups["dstring"].Value)
                {
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Foreground = StringBrush,
                });
            }
            else if (match.Groups["sstring"].Success)
            {
                inlines.Add(new Run(match.Groups["sstring"].Value)
                {
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Foreground = StringBrush,
                });
            }
            else if (match.Groups["number"].Success)
            {
                inlines.Add(new Run(match.Groups["number"].Value)
                {
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Foreground = NumberBrush,
                });
            }
            else if (match.Groups["ident"].Success)
            {
                var word = match.Groups["ident"].Value;
                // Check if next character is '(' → function call
                var nextChar = match.Index + match.Length < code.Length
                    ? code[match.Index + match.Length]
                    : '\0';

                if (LuaKeywords.Contains(word))
                {
                    inlines.Add(new Run(word)
                    {
                        FontFamily = MonoFont,
                        FontSize = 12,
                        Foreground = KeywordBrush,
                        FontWeight = FontWeight.Bold,
                    });
                }
                else if (LuaGlobals.Contains(word))
                {
                    inlines.Add(new Run(word)
                    {
                        FontFamily = MonoFont,
                        FontSize = 12,
                        Foreground = KeywordBrush,
                    });
                }
                else if (nextChar == '(')
                {
                    inlines.Add(new Run(word)
                    {
                        FontFamily = MonoFont,
                        FontSize = 12,
                        Foreground = FunctionBrush,
                    });
                }
                else
                {
                    inlines.Add(new Run(word)
                    {
                        FontFamily = MonoFont,
                        FontSize = 12,
                        Foreground = CodeBrush,
                    });
                }
            }
            else if (match.Groups["whitespace"].Success)
            {
                inlines.Add(new Run(match.Groups["whitespace"].Value)
                {
                    FontFamily = MonoFont,
                    FontSize = 12,
                });
            }
            else if (match.Groups["other"].Success)
            {
                inlines.Add(new Run(match.Groups["other"].Value)
                {
                    FontFamily = MonoFont,
                    FontSize = 12,
                    Foreground = CodeBrush,
                });
            }
        }
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

    // ── Table support ──────────────────────────────────────────

    private static bool IsTableRow(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Contains('|') && trimmed.Length > 1;
    }

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.Contains('|') || !trimmed.Contains('-'))
            return false;
        // Each cell between pipes must be only dashes, colons, and spaces.
        var cells = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (cells.Length == 0) return false;
        foreach (var cell in cells)
        {
            var c = cell.Trim();
            if (c.Length == 0) return false;
            if (!c.All(ch => ch == '-' || ch == ':' || ch == ' '))
                return false;
            if (!c.Contains('-'))
                return false;
        }
        return true;
    }

    /// <summary>
    ///   Splits a table row into cell contents, stripping leading/trailing
    ///   pipes and whitespace. Handles escaped pipes (\|) inside cells.
    /// </summary>
    private static string[] SplitTableRow(string line)
    {
        var trimmed = line.Trim().TrimStart('|').TrimEnd('|');
        // Split on | but not \|
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        for (var j = 0; j < trimmed.Length; j++)
        {
            if (trimmed[j] == '\\' && j + 1 < trimmed.Length && trimmed[j + 1] == '|')
            {
                current.Append('|');
                j++;
            }
            else if (trimmed[j] == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(trimmed[j]);
            }
        }
        cells.Add(current.ToString().Trim());
        return cells.ToArray();
    }

    /// <summary>
    ///   Renders a table as monospace, padded text with a separator line.
    ///   Columns are auto-sized to the widest cell.
    /// </summary>
    private static void AddTable(InlineCollection inlines, List<string> rows)
    {
        // Parse all rows into cells
        var allCells = rows.Select(SplitTableRow).ToArray();
        var numCols = allCells.Max(c => c.Length);

        // Calculate column widths
        var colWidths = new int[numCols];
        for (var col = 0; col < numCols; col++)
        {
            colWidths[col] = 0;
            for (var row = 0; row < allCells.Length; row++)
            {
                if (col < allCells[row].Length)
                    colWidths[col] = Math.Max(colWidths[col], allCells[row][col].Length);
            }
            colWidths[col] = Math.Max(colWidths[col], 3); // minimum width
        }

        // Render header row
        var headerCells = allCells[0];
        var headerLine = BuildTableRow(headerCells, colWidths);
        inlines.Add(new Run(headerLine + "\n")
        {
            FontFamily = MonoFont,
            FontSize = 12,
            FontWeight = FontWeight.Bold,
        });

        // Render separator line
        var sepLine = BuildTableSeparator(colWidths);
        inlines.Add(new Run(sepLine + "\n")
        {
            FontFamily = MonoFont,
            FontSize = 12,
            Foreground = HrColorBrush,
        });

        // Render data rows (skip header at 0 and separator at 1)
        for (var row = 2; row < allCells.Length; row++)
        {
            var dataLine = BuildTableRow(allCells[row], colWidths);
            inlines.Add(new Run(dataLine + "\n")
            {
                FontFamily = MonoFont,
                FontSize = 12,
            });
        }
    }

    private static string BuildTableRow(string[] cells, int[] colWidths)
    {
        var sb = new System.Text.StringBuilder();
        for (var col = 0; col < colWidths.Length; col++)
        {
            var cellText = col < cells.Length ? cells[col] : "";
            sb.Append(" ").Append(cellText.PadRight(colWidths[col])).Append(" ");
            if (col < colWidths.Length - 1)
                sb.Append("|");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildTableSeparator(int[] colWidths)
    {
        var sb = new System.Text.StringBuilder();
        for (var col = 0; col < colWidths.Length; col++)
        {
            sb.Append(" ").Append(new string('-', colWidths[col])).Append(" ");
            if (col < colWidths.Length - 1)
                sb.Append("|");
        }
        return sb.ToString().TrimEnd();
    }

    private static readonly Regex InlineFormatRegex = new(
        @"(\*\*(.+?)\*\*)|(`(.+?)`)|(~~(.+?)~~)|(\*(.+?)\*)|(\[([^\]]+)\]\(([^)]+)\))",
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
                    Foreground = CodeBrush,
                });
            }
            else if (match.Groups[6].Success) // ~~strikethrough~~
            {
                inlines.Add(new Run(match.Groups[6].Value)
                {
                    TextDecorations = TextDecorations.Strikethrough,
                    Foreground = QuoteBrush,
                });
            }
            else if (match.Groups[8].Success) // *italic*
            {
                inlines.Add(new Run(match.Groups[8].Value) { FontStyle = FontStyle.Italic });
            }
            else if (match.Groups[10].Success) // [text](url)
            {
                var linkText = match.Groups[10].Value;
                var url = match.Groups[11].Value;
                // Render as a styled run with the URL in a tooltip-like suffix.
                // Truly clickable links in SelectableTextBlock require InlineUIContainer
                // which breaks text selection, so we show the URL inline instead.
                inlines.Add(new Run(linkText)
                {
                    Foreground = LinkBrush,
                    TextDecorations = TextDecorations.Underline,
                });
                inlines.Add(new Run($" ({url})")
                {
                    FontSize = 10,
                    Foreground = QuoteBrush,
                });
            }

            pos = match.Index + match.Length;
        }

        if (pos < text.Length)
            inlines.Add(new Run(text[pos..]));
    }
}
