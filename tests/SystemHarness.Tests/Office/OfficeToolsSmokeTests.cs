using SystemHarness.Apps.Office;
using SystemHarness.Mcp.Tools;

namespace SystemHarness.Tests.Office;

/// <summary>
/// End-to-end smoke tests for OfficeTools MCP wrappers.
/// Write → Read round-trip + find/replace for each document type.
/// </summary>
[Trait("Category", "CI")]
public class OfficeToolsSmokeTests : IDisposable
{
    private readonly OfficeTools _tools;
    private readonly string _tempDir;

    public OfficeToolsSmokeTests()
    {
        var docReader = new OpenXmlDocumentReader();
        var hwpReader = new HwpxReader();
        _tools = new OfficeTools(docReader, hwpReader);
        _tempDir = Path.Combine(Path.GetTempPath(), "sh-mcp-smoke-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string TempPath(string name) => Path.Combine(_tempDir, name);

    // ── Word ──

    [Fact]
    public async Task Word_WriteAndRead_RoundTrip()
    {
        var path = TempPath("test.docx");

        var writeResult = await _tools.WriteWordAsync(path, """
        {
            "paragraphs": [
                { "text": "Project Report", "style": "Heading1" },
                { "text": "This is the introduction paragraph." },
                { "text": "Key Findings", "style": "Heading2" },
                { "text": "Finding one", "listType": "Bullet" },
                { "text": "Finding two", "listType": "Bullet" }
            ]
        }
        """);

        Assert.Contains("5 paragraphs", writeResult);
        Assert.True(File.Exists(path));

        var markdown = await _tools.ReadWordAsync(path);
        Assert.Contains("# Project Report", markdown);
        Assert.Contains("This is the introduction paragraph.", markdown);
        Assert.Contains("## Key Findings", markdown);
        Assert.Contains("- Finding one", markdown);
        Assert.Contains("- Finding two", markdown);
    }

    [Fact]
    public async Task Word_FindReplace()
    {
        var path = TempPath("replace.docx");

        await _tools.WriteWordAsync(path, """
        {
            "paragraphs": [
                { "text": "Hello World" },
                { "text": "Hello again" }
            ]
        }
        """);

        var result = await _tools.FindReplaceWordAsync(path, "Hello", "Hi");
        Assert.Contains("\"count\":2", result);

        var markdown = await _tools.ReadWordAsync(path);
        Assert.Contains("Hi World", markdown);
        Assert.Contains("Hi again", markdown);
    }

    // ── Excel ──

    [Fact]
    public async Task Excel_WriteAndRead_RoundTrip()
    {
        var path = TempPath("test.xlsx");

        var writeResult = await _tools.WriteExcelAsync(path, """
        {
            "sheets": [
                {
                    "name": "Sales",
                    "rows": [
                        ["Product", "Q1", "Q2", "Q3"],
                        ["Widget A", "100", "150", "200"],
                        ["Widget B", "80", "90", "120"]
                    ]
                },
                {
                    "name": "Summary",
                    "rows": [
                        ["Metric", "Value"],
                        ["Total Revenue", "740"]
                    ]
                }
            ]
        }
        """);

        Assert.Contains("2 sheet(s)", writeResult);
        Assert.Contains("5 total rows", writeResult);
        Assert.True(File.Exists(path));

        var markdown = await _tools.ReadExcelAsync(path);
        Assert.Contains("Sales", markdown);
        Assert.Contains("Product", markdown);
        Assert.Contains("Widget A", markdown);
        Assert.Contains("Summary", markdown);
        Assert.Contains("Total Revenue", markdown);
    }

    // ── PowerPoint ──

    [Fact]
    public async Task PowerPoint_WriteAndRead_RoundTrip()
    {
        var path = TempPath("test.pptx");

        var writeResult = await _tools.WritePowerPointAsync(path, """
        {
            "slides": [
                { "texts": ["Annual Review 2025", "Company Performance"] },
                { "texts": ["Revenue Growth", "Revenue increased by 25% YoY"] },
                { "texts": ["Next Steps"], "notes": "Discuss timeline" }
            ]
        }
        """);

        Assert.Contains("3 slide(s)", writeResult);
        Assert.True(File.Exists(path));

        var markdown = await _tools.ReadPowerPointAsync(path);
        Assert.Contains("Slide 1", markdown);
        Assert.Contains("Annual Review 2025", markdown);
        Assert.Contains("Revenue Growth", markdown);
        Assert.Contains("Next Steps", markdown);
        Assert.Contains("Notes:", markdown);
        Assert.Contains("Discuss timeline", markdown);
    }

    // ── HWPX ──

    [Fact]
    public async Task Hwpx_WriteAndRead_RoundTrip()
    {
        var path = TempPath("test.hwpx");

        var writeResult = await _tools.WriteHwpxAsync(path, """
        {
            "sections": [
                {
                    "paragraphs": [
                        { "text": "회의록" },
                        { "text": "2025년 1월 15일 진행된 정기 회의입니다." },
                        { "text": "참석자: 김철수, 이영희, 박민수" }
                    ]
                }
            ]
        }
        """);

        Assert.Contains("1 section(s)", writeResult);
        Assert.Contains("3 paragraphs", writeResult);
        Assert.True(File.Exists(path));

        var markdown = await _tools.ReadHwpxAsync(path);
        Assert.Contains("회의록", markdown);
        Assert.Contains("2025년 1월 15일", markdown);
        Assert.Contains("참석자", markdown);
    }

    [Fact]
    public async Task Hwpx_FindReplace()
    {
        var path = TempPath("replace.hwpx");

        await _tools.WriteHwpxAsync(path, """
        {
            "sections": [
                {
                    "paragraphs": [
                        { "text": "이름: 홍길동" },
                        { "text": "담당자: 홍길동" }
                    ]
                }
            ]
        }
        """);

        var result = await _tools.FindReplaceHwpxAsync(path, "홍길동", "김철수");
        Assert.Contains("\"count\":2", result);

        var markdown = await _tools.ReadHwpxAsync(path);
        Assert.Contains("김철수", markdown);
    }

    // ── Error Cases ──

    [Fact]
    public async Task ReadWord_NonexistentFile_Throws()
    {
        var path = TempPath("nonexistent.docx");
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _tools.ReadWordAsync(path));
    }

    [Fact]
    public async Task WriteWord_InvalidJson_ReturnsError()
    {
        var path = TempPath("bad.docx");
        var result = await _tools.WriteWordAsync(path, "not valid json{{");
        Assert.Contains("invalid_parameter", result);
    }

    [Fact]
    public async Task WriteWord_AutoCreatesDirectory()
    {
        var path = Path.Combine(_tempDir, "nested", "deep", "auto.docx");

        await _tools.WriteWordAsync(path, """{"paragraphs": [{"text": "Hello"}]}""");

        Assert.True(File.Exists(path));
        var markdown = await _tools.ReadWordAsync(path);
        Assert.Contains("Hello", markdown);
    }

    [Fact]
    public async Task Word_NumberedList_RoundTrip()
    {
        var path = TempPath("numbered.docx");

        await _tools.WriteWordAsync(path, """
        {
            "paragraphs": [
                { "text": "Steps", "style": "Heading1" },
                { "text": "First step", "listType": "Numbered" },
                { "text": "Second step", "listType": "Numbered" }
            ]
        }
        """);

        var markdown = await _tools.ReadWordAsync(path);
        Assert.Contains("1. First step", markdown);
        Assert.Contains("1. Second step", markdown);
    }

    // ── Validation Tests ──

    [Fact]
    public async Task WriteWord_WrongExtension_ReturnsError()
    {
        var path = TempPath("wrong.xlsx");
        var result = await _tools.WriteWordAsync(path, """{"paragraphs": [{"text": "Hi"}]}""");
        Assert.Contains("invalid_parameter", result);
        Assert.Contains("Expected .docx", result);
    }

    [Fact]
    public async Task WriteWord_EmptyParagraphs_ReturnsError()
    {
        var path = TempPath("empty.docx");
        var result = await _tools.WriteWordAsync(path, """{"paragraphs": []}""");
        Assert.Contains("invalid_parameter", result);
        Assert.Contains("at least one paragraph", result);
    }

    [Fact]
    public async Task WriteExcel_EmptySheets_ReturnsError()
    {
        var path = TempPath("empty.xlsx");
        var result = await _tools.WriteExcelAsync(path, """{"sheets": []}""");
        Assert.Contains("invalid_parameter", result);
        Assert.Contains("at least one sheet", result);
    }

    [Fact]
    public async Task ReadWord_WrongExtension_ReturnsError()
    {
        var path = TempPath("wrong.pptx");
        var result = await _tools.ReadWordAsync(path);
        Assert.Contains("invalid_parameter", result);
        Assert.Contains("Expected .docx", result);
    }
}
