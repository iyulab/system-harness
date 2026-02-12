using System.Text;

namespace SystemHarness.Apps.Office;

/// <summary>
/// Extension methods for converting document content to plain text and markdown.
/// </summary>
public static class DocumentExtensions
{
    // ── Word ──

    /// <summary>
    /// Extract all text from a Word document as a single string with paragraph breaks.
    /// </summary>
    public static string ToPlainText(this DocumentContent content)
    {
        var sb = new StringBuilder();

        foreach (var para in content.Paragraphs)
        {
            sb.AppendLine(para.Text);
        }

        foreach (var table in content.Tables)
        {
            sb.AppendLine();
            AppendTableText(sb, table.Rows);
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Convert Word document content to markdown.
    /// Supports headings, bold, italic, hyperlinks, tables, and lists.
    /// </summary>
    public static string ToMarkdown(this DocumentContent content)
    {
        var sb = new StringBuilder();

        foreach (var para in content.Paragraphs)
        {
            var line = FormatParagraphMarkdown(para);
            sb.AppendLine(line);
            sb.AppendLine();
        }

        foreach (var table in content.Tables)
        {
            AppendMarkdownTable(sb, table.Rows);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    // ── Excel ──

    /// <summary>
    /// Extract all text from an Excel spreadsheet as tab-separated values.
    /// </summary>
    public static string ToPlainText(this SpreadsheetContent content)
    {
        var sb = new StringBuilder();

        foreach (var sheet in content.Sheets)
        {
            if (content.Sheets.Count > 1)
                sb.AppendLine($"[{sheet.Name}]");

            foreach (var row in sheet.Rows)
            {
                sb.AppendLine(string.Join('\t', row));
            }

            if (content.Sheets.Count > 1)
                sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Convert Excel spreadsheet content to markdown tables.
    /// </summary>
    public static string ToMarkdown(this SpreadsheetContent content)
    {
        var sb = new StringBuilder();

        foreach (var sheet in content.Sheets)
        {
            if (content.Sheets.Count > 1)
            {
                sb.AppendLine($"## {sheet.Name}");
                sb.AppendLine();
            }

            if (sheet.Rows.Count > 0)
            {
                AppendMarkdownTable(sb, sheet.Rows);
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    // ── PowerPoint ──

    /// <summary>
    /// Extract all text from a PowerPoint presentation.
    /// </summary>
    public static string ToPlainText(this PresentationContent content)
    {
        var sb = new StringBuilder();

        foreach (var slide in content.Slides)
        {
            sb.AppendLine($"--- Slide {slide.Number} ---");
            foreach (var text in slide.Texts)
            {
                sb.AppendLine(text);
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Convert PowerPoint presentation content to markdown.
    /// </summary>
    public static string ToMarkdown(this PresentationContent content)
    {
        var sb = new StringBuilder();

        foreach (var slide in content.Slides)
        {
            sb.AppendLine($"## Slide {slide.Number}");
            sb.AppendLine();
            foreach (var text in slide.Texts)
            {
                sb.AppendLine(text);
                sb.AppendLine();
            }
            if (slide.Notes is not null)
            {
                sb.AppendLine($"> **Notes:** {slide.Notes}");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    // ── HWP ──

    /// <summary>
    /// Extract all text from a HWPX document as a single string.
    /// </summary>
    public static string ToPlainText(this HwpContent content)
    {
        var sb = new StringBuilder();

        foreach (var section in content.Sections)
        {
            foreach (var para in section.Paragraphs)
            {
                sb.AppendLine(para.Text);
            }

            foreach (var table in section.Tables)
            {
                sb.AppendLine();
                AppendTableText(sb, table.Rows);
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Convert HWPX document content to markdown.
    /// </summary>
    public static string ToMarkdown(this HwpContent content)
    {
        var sb = new StringBuilder();

        for (int s = 0; s < content.Sections.Count; s++)
        {
            var section = content.Sections[s];

            if (content.Sections.Count > 1)
            {
                sb.AppendLine($"## Section {s + 1}");
                sb.AppendLine();
            }

            foreach (var para in section.Paragraphs)
            {
                var line = FormatHwpParagraphMarkdown(para);
                sb.AppendLine(line);
                sb.AppendLine();
            }

            foreach (var table in section.Tables)
            {
                AppendMarkdownTable(sb, table.Rows);
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    // ── Private Helpers ──

    private static string FormatParagraphMarkdown(DocumentParagraph para)
    {
        // Heading styles → # markers
        var prefix = para.Style switch
        {
            "Heading1" => "# ",
            "Heading2" => "## ",
            "Heading3" => "### ",
            "Heading4" => "#### ",
            _ => "",
        };

        // List items (with indentation for nested levels)
        if (para.ListType == ListType.Bullet)
        {
            var indent = new string(' ', (para.ListLevel ?? 0) * 2);
            prefix = indent + "- ";
        }
        else if (para.ListType == ListType.Numbered)
        {
            var indent = new string(' ', (para.ListLevel ?? 0) * 2);
            prefix = indent + "1. ";
        }

        // If runs have formatting, build from runs
        if (para.Runs.Count > 0)
        {
            var sb = new StringBuilder(prefix);
            foreach (var run in para.Runs)
            {
                sb.Append(FormatRunMarkdown(run));
            }
            return sb.ToString();
        }

        return prefix + para.Text;
    }

    private static string FormatRunMarkdown(DocumentRun run)
    {
        var text = run.Text;

        if (run.Bold && run.Italic)
            text = $"***{text}***";
        else if (run.Bold)
            text = $"**{text}**";
        else if (run.Italic)
            text = $"*{text}*";

        if (run.HyperlinkUri is not null)
            text = $"[{text}]({run.HyperlinkUri})";

        return text;
    }

    private static string FormatHwpParagraphMarkdown(HwpParagraph para)
    {
        if (para.Runs.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var run in para.Runs)
            {
                var text = run.Text;
                if (run.Bold && run.Italic)
                    text = $"***{text}***";
                else if (run.Bold)
                    text = $"**{text}**";
                else if (run.Italic)
                    text = $"*{text}*";
                sb.Append(text);
            }
            return sb.ToString();
        }

        return para.Text;
    }

    private static void AppendTableText(StringBuilder sb, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join('\t', row));
        }
    }

    private static void AppendMarkdownTable(StringBuilder sb, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0) return;

        var colCount = rows.Max(r => r.Count);
        if (colCount == 0) return;

        // Header row
        var headerRow = rows[0];
        sb.Append('|');
        for (int c = 0; c < colCount; c++)
        {
            sb.Append(' ');
            sb.Append(c < headerRow.Count ? EscapeMarkdownCell(headerRow[c]) : "");
            sb.Append(" |");
        }
        sb.AppendLine();

        // Separator row
        sb.Append('|');
        for (int c = 0; c < colCount; c++)
        {
            sb.Append(" --- |");
        }
        sb.AppendLine();

        // Data rows
        for (int r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            sb.Append('|');
            for (int c = 0; c < colCount; c++)
            {
                sb.Append(' ');
                sb.Append(c < row.Count ? EscapeMarkdownCell(row[c]) : "");
                sb.Append(" |");
            }
            sb.AppendLine();
        }
    }

    private static string EscapeMarkdownCell(string text)
    {
        return text.Replace("|", "\\|").Replace("\n", " ");
    }
}
