using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using SystemHarness.Apps.Office;

namespace SystemHarness.Mcp.Tools;

public sealed class OfficeTools(IDocumentReader docReader, IHwpReader hwpReader)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Read Tools ──

    [McpServerTool(Name = "office_read_word"), Description(
        "Read a .docx Word document and return its content as markdown. " +
        "Includes headings, paragraphs, bold/italic formatting, lists, tables, and hyperlinks.")]
    public async Task<string> ReadWordAsync(
        [Description("Path to .docx file.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".docx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        var content = await docReader.ReadWordAsync(path, ct);
        return McpResponse.Content(content.ToMarkdown(), "markdown", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "office_read_excel"), Description(
        "Read a .xlsx Excel spreadsheet and return its content as markdown tables. " +
        "Each sheet becomes a separate section. Includes all cell values.")]
    public async Task<string> ReadExcelAsync(
        [Description("Path to .xlsx file.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".xlsx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        var content = await docReader.ReadExcelAsync(path, ct);
        return McpResponse.Content(content.ToMarkdown(), "markdown", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "office_read_pptx"), Description(
        "Read a .pptx PowerPoint presentation and return its content as markdown. " +
        "Each slide becomes a section with its text content.")]
    public async Task<string> ReadPowerPointAsync(
        [Description("Path to .pptx file.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".pptx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        var content = await docReader.ReadPowerPointAsync(path, ct);
        return McpResponse.Content(content.ToMarkdown(), "markdown", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "office_read_hwpx"), Description(
        "Read a .hwpx (Korean HWP) document and return its content as markdown. " +
        "HWPX is the Open Word Processor Markup Language format (KS X 6101).")]
    public async Task<string> ReadHwpxAsync(
        [Description("Path to .hwpx file.")] string path, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".hwpx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        var content = await hwpReader.ReadHwpxAsync(path, ct);
        return McpResponse.Content(content.ToMarkdown(), "markdown", sw.ElapsedMilliseconds);
    }

    // ── Write Tools ──

    [McpServerTool(Name = "office_write_word"), Description(
        "Create or overwrite a .docx Word document. " +
        "contentJson is a JSON string with: " +
        "{\"paragraphs\": [{\"text\": \"Hello\", \"style\": \"Heading1\"}, {\"text\": \"Body text\"}]}. " +
        "Supported styles: Heading1, Heading2, Heading3, Heading4, Normal (default). " +
        "Optional per-paragraph: \"listType\" (\"Bullet\" or \"Numbered\"), \"listLevel\" (0-based).")]
    public async Task<string> WriteWordAsync(
        [Description("Output .docx file path.")] string path,
        [Description("JSON string with document content. See tool description for schema.")] string contentJson,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".docx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        var (input, deserializeError) = TryDeserializeInput<WordInput>(contentJson, "Word document", sw.ElapsedMilliseconds);
        if (input is null) return deserializeError!;
        if (input.Paragraphs.Count == 0)
            return McpResponse.Error("invalid_parameter", "Word document must contain at least one paragraph.", sw.ElapsedMilliseconds);
        EnsureDirectoryExists(path);

        var content = new DocumentContent
        {
            Paragraphs = input.Paragraphs.Select(p => new DocumentParagraph
            {
                Text = p.Text,
                Style = p.Style,
                ListType = p.ListType switch
                {
                    "Bullet" => ListType.Bullet,
                    "Numbered" => ListType.Numbered,
                    _ => null,
                },
                ListLevel = p.ListLevel,
            }).ToList(),
        };

        await docReader.WriteWordAsync(path, content, ct);
        ActionLog.Record("office_write_word", $"path={path}, paragraphs={content.Paragraphs.Count}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Word document written to {path} ({content.Paragraphs.Count} paragraphs).", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "office_write_excel"), Description(
        "Create or overwrite a .xlsx Excel spreadsheet. " +
        "contentJson is a JSON string with: " +
        "{\"sheets\": [{\"name\": \"Sheet1\", \"rows\": [[\"A\",\"B\"],[\"1\",\"2\"]]}]}. " +
        "First row is typically headers. Each row is an array of cell value strings.")]
    public async Task<string> WriteExcelAsync(
        [Description("Output .xlsx file path.")] string path,
        [Description("JSON string with spreadsheet content. See tool description for schema.")] string contentJson,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".xlsx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        var (input, deserializeError) = TryDeserializeInput<ExcelInput>(contentJson, "Excel spreadsheet", sw.ElapsedMilliseconds);
        if (input is null) return deserializeError!;
        if (input.Sheets.Count == 0)
            return McpResponse.Error("invalid_parameter", "Excel spreadsheet must contain at least one sheet.", sw.ElapsedMilliseconds);
        EnsureDirectoryExists(path);

        var content = new SpreadsheetContent
        {
            Sheets = input.Sheets.Select(s => new SpreadsheetSheet
            {
                Name = s.Name,
                Rows = s.Rows.Select(r => (IReadOnlyList<string>)r.ToList()).ToList(),
            }).ToList(),
        };

        await docReader.WriteExcelAsync(path, content, ct);
        var totalRows = content.Sheets.Sum(s => s.Rows.Count);
        ActionLog.Record("office_write_excel", $"path={path}, sheets={content.Sheets.Count}, rows={totalRows}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Excel spreadsheet written to {path} ({content.Sheets.Count} sheet(s), {totalRows} total rows).", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "office_write_pptx"), Description(
        "Create or overwrite a .pptx PowerPoint presentation. " +
        "contentJson is a JSON string with: " +
        "{\"slides\": [{\"texts\": [\"Title\", \"Subtitle\"]}, {\"texts\": [\"Slide 2 content\"]}]}. " +
        "Each slide has an array of text strings.")]
    public async Task<string> WritePowerPointAsync(
        [Description("Output .pptx file path.")] string path,
        [Description("JSON string with presentation content. See tool description for schema.")] string contentJson,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".pptx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        var (input, deserializeError) = TryDeserializeInput<PowerPointInput>(contentJson, "PowerPoint presentation", sw.ElapsedMilliseconds);
        if (input is null) return deserializeError!;
        if (input.Slides.Count == 0)
            return McpResponse.Error("invalid_parameter", "PowerPoint presentation must contain at least one slide.", sw.ElapsedMilliseconds);
        EnsureDirectoryExists(path);

        var content = new PresentationContent
        {
            Slides = input.Slides.Select((s, i) => new PresentationSlide
            {
                Number = i + 1,
                Texts = s.Texts,
                Notes = s.Notes,
            }).ToList(),
        };

        await docReader.WritePowerPointAsync(path, content, ct);
        ActionLog.Record("office_write_pptx", $"path={path}, slides={content.Slides.Count}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"PowerPoint presentation written to {path} ({content.Slides.Count} slide(s)).", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "office_write_hwpx"), Description(
        "Create or overwrite a .hwpx (Korean HWP) document. " +
        "contentJson is a JSON string with: " +
        "{\"sections\": [{\"paragraphs\": [{\"text\": \"Hello\"}, {\"text\": \"World\"}]}]}. " +
        "Most documents use a single section.")]
    public async Task<string> WriteHwpxAsync(
        [Description("Output .hwpx file path.")] string path,
        [Description("JSON string with HWPX content. See tool description for schema.")] string contentJson,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".hwpx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        var (input, deserializeError) = TryDeserializeInput<HwpxInput>(contentJson, "HWPX document", sw.ElapsedMilliseconds);
        if (input is null) return deserializeError!;
        if (input.Sections.Count == 0)
            return McpResponse.Error("invalid_parameter", "HWPX document must contain at least one section.", sw.ElapsedMilliseconds);
        EnsureDirectoryExists(path);

        var content = new HwpContent
        {
            Sections = input.Sections.Select(s => new HwpSection
            {
                Paragraphs = s.Paragraphs.Select(p => new HwpParagraph
                {
                    Text = p.Text,
                }).ToList(),
            }).ToList(),
        };

        await hwpReader.WriteHwpxAsync(path, content, ct);
        var totalParas = content.Sections.Sum(s => s.Paragraphs.Count);
        ActionLog.Record("office_write_hwpx", $"path={path}, sections={content.Sections.Count}, paragraphs={totalParas}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"HWPX document written to {path} ({content.Sections.Count} section(s), {totalParas} paragraphs).", sw.ElapsedMilliseconds);
    }

    // ── Find/Replace Tools ──

    [McpServerTool(Name = "office_replace_word"), Description(
        "Find and replace text in a .docx Word document (modified in-place). " +
        "Returns the number of replacements made.")]
    public async Task<string> FindReplaceWordAsync(
        [Description("Path to .docx file to modify.")] string path,
        [Description("Text to find.")] string find,
        [Description("Replacement text.")] string replace,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".docx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        if (string.IsNullOrWhiteSpace(find))
            return McpResponse.Error("invalid_parameter", "find text cannot be empty.", sw.ElapsedMilliseconds);
        var count = await docReader.FindReplaceWordAsync(path, find, replace, ct);
        ActionLog.Record("office_replace_word", $"path={path}, replacements={count}", sw.ElapsedMilliseconds, true);
        return McpResponse.Ok(new { count, path, find, replace }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "office_replace_hwpx"), Description(
        "Find and replace text in a .hwpx document (modified in-place). " +
        "Returns the number of replacements made.")]
    public async Task<string> FindReplaceHwpxAsync(
        [Description("Path to .hwpx file to modify.")] string path,
        [Description("Text to find.")] string find,
        [Description("Replacement text.")] string replace,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var error = ValidateExtension(path, ".hwpx", sw.ElapsedMilliseconds);
        if (error is not null) return error;
        if (string.IsNullOrWhiteSpace(find))
            return McpResponse.Error("invalid_parameter", "find text cannot be empty.", sw.ElapsedMilliseconds);
        var count = await hwpReader.FindReplaceAsync(path, find, replace, ct);
        ActionLog.Record("office_replace_hwpx", $"path={path}, replacements={count}", sw.ElapsedMilliseconds, true);
        return McpResponse.Ok(new { count, path, find, replace }, sw.ElapsedMilliseconds);
    }

    // ── Helpers ──

    private static string? ValidateExtension(string path, string expected, long elapsedMs)
    {
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", elapsedMs);
        var ext = Path.GetExtension(path);
        if (!ext.Equals(expected, StringComparison.OrdinalIgnoreCase))
            return McpResponse.Error("invalid_parameter",
                $"Expected {expected} file, got '{ext}'. Path: {path}", elapsedMs);
        return null;
    }

    private static void EnsureDirectoryExists(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
            Directory.CreateDirectory(dir);
    }

    private static (T? Result, string? Error) TryDeserializeInput<T>(string json, string documentType, long elapsedMs) where T : class
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return result is null
                ? (null, McpResponse.Error("invalid_parameter", $"contentJson deserialized to null for {documentType}.", elapsedMs))
                : (result, null);
        }
        catch (JsonException ex)
        {
            return (null, McpResponse.Error("invalid_parameter", $"Invalid contentJson for {documentType}: {ex.Message}", elapsedMs));
        }
    }

    // ── JSON Input Models ──

    private sealed class WordInput
    {
        public List<WordParagraphInput> Paragraphs { get; set; } = [];
    }

    private sealed class WordParagraphInput
    {
        public string Text { get; set; } = "";
        public string? Style { get; set; }
        public string? ListType { get; set; }
        public int? ListLevel { get; set; }
    }

    private sealed class ExcelInput
    {
        public List<ExcelSheetInput> Sheets { get; set; } = [];
    }

    private sealed class ExcelSheetInput
    {
        public string Name { get; set; } = "Sheet1";
        public List<List<string>> Rows { get; set; } = [];
    }

    private sealed class PowerPointInput
    {
        public List<PowerPointSlideInput> Slides { get; set; } = [];
    }

    private sealed class PowerPointSlideInput
    {
        public List<string> Texts { get; set; } = [];
        public string? Notes { get; set; }
    }

    private sealed class HwpxInput
    {
        public List<HwpxSectionInput> Sections { get; set; } = [];
    }

    private sealed class HwpxSectionInput
    {
        public List<HwpxParagraphInput> Paragraphs { get; set; } = [];
    }

    private sealed class HwpxParagraphInput
    {
        public string Text { get; set; } = "";
    }
}
