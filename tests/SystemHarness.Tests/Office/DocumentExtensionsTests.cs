using SystemHarness.Apps.Office;

namespace SystemHarness.Tests.Office;

[Trait("Category", "CI")]
public class DocumentExtensionsTests
{
    // ── Word ToPlainText ──

    [Fact]
    public void Word_ToPlainText_CombinesParagraphs()
    {
        var content = new DocumentContent
        {
            Paragraphs = [
                new DocumentParagraph { Text = "Hello" },
                new DocumentParagraph { Text = "World" },
            ],
        };

        var text = content.ToPlainText();
        Assert.Contains("Hello", text);
        Assert.Contains("World", text);
    }

    [Fact]
    public void Word_ToPlainText_IncludesTables()
    {
        var content = new DocumentContent
        {
            Tables = [
                new DocumentTable
                {
                    Rows = [(IReadOnlyList<string>)["A", "B"]],
                },
            ],
        };

        var text = content.ToPlainText();
        Assert.Contains("A", text);
        Assert.Contains("B", text);
    }

    // ── Word ToMarkdown ──

    [Fact]
    public void Word_ToMarkdown_HeadingStyles()
    {
        var content = new DocumentContent
        {
            Paragraphs = [
                new DocumentParagraph { Text = "Title", Style = "Heading1" },
                new DocumentParagraph { Text = "Section", Style = "Heading2" },
                new DocumentParagraph { Text = "Body text" },
            ],
        };

        var md = content.ToMarkdown();
        Assert.Contains("# Title", md);
        Assert.Contains("## Section", md);
        Assert.Contains("Body text", md);
    }

    [Fact]
    public void Word_ToMarkdown_BoldItalic()
    {
        var content = new DocumentContent
        {
            Paragraphs = [
                new DocumentParagraph
                {
                    Text = "Bold and italic",
                    Runs = [
                        new DocumentRun { Text = "Bold", Bold = true },
                        new DocumentRun { Text = " normal " },
                        new DocumentRun { Text = "italic", Italic = true },
                    ],
                },
            ],
        };

        var md = content.ToMarkdown();
        Assert.Contains("**Bold**", md);
        Assert.Contains("*italic*", md);
    }

    [Fact]
    public void Word_ToMarkdown_Tables()
    {
        var content = new DocumentContent
        {
            Tables = [
                new DocumentTable
                {
                    Rows = [
                        (IReadOnlyList<string>)["Name", "Value"],
                        (IReadOnlyList<string>)["Alice", "100"],
                    ],
                },
            ],
        };

        var md = content.ToMarkdown();
        Assert.Contains("| Name | Value |", md);
        Assert.Contains("| --- |", md);
        Assert.Contains("| Alice | 100 |", md);
    }

    [Fact]
    public void Word_ToMarkdown_BulletList()
    {
        var content = new DocumentContent
        {
            Paragraphs = [
                new DocumentParagraph { Text = "Item one", ListType = ListType.Bullet },
                new DocumentParagraph { Text = "Item two", ListType = ListType.Bullet },
            ],
        };

        var md = content.ToMarkdown();
        Assert.Contains("- Item one", md);
        Assert.Contains("- Item two", md);
    }

    // ── Excel ──

    [Fact]
    public void Excel_ToPlainText_TabSeparated()
    {
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet
                {
                    Name = "Sheet1",
                    Rows = [(IReadOnlyList<string>)["A1", "B1"]],
                },
            ],
        };

        var text = content.ToPlainText();
        Assert.Contains("A1\tB1", text);
    }

    [Fact]
    public void Excel_ToPlainText_MultipleSheets()
    {
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet { Name = "Data", Rows = [(IReadOnlyList<string>)["X"]] },
                new SpreadsheetSheet { Name = "Summary", Rows = [(IReadOnlyList<string>)["Y"]] },
            ],
        };

        var text = content.ToPlainText();
        Assert.Contains("[Data]", text);
        Assert.Contains("[Summary]", text);
    }

    [Fact]
    public void Excel_ToMarkdown_Table()
    {
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet
                {
                    Name = "Data",
                    Rows = [
                        (IReadOnlyList<string>)["Col1", "Col2"],
                        (IReadOnlyList<string>)["A", "B"],
                    ],
                },
            ],
        };

        var md = content.ToMarkdown();
        Assert.Contains("| Col1 | Col2 |", md);
        Assert.Contains("| A | B |", md);
    }

    // ── PowerPoint ──

    [Fact]
    public void PowerPoint_ToPlainText_SlideHeaders()
    {
        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide { Number = 1, Texts = ["Title", "Body"] },
                new PresentationSlide { Number = 2, Texts = ["Slide 2"] },
            ],
        };

        var text = content.ToPlainText();
        Assert.Contains("--- Slide 1 ---", text);
        Assert.Contains("Title", text);
        Assert.Contains("--- Slide 2 ---", text);
    }

    [Fact]
    public void PowerPoint_ToMarkdown_SlideHeadings()
    {
        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide { Number = 1, Texts = ["Hello"] },
            ],
        };

        var md = content.ToMarkdown();
        Assert.Contains("## Slide 1", md);
        Assert.Contains("Hello", md);
    }

    // ── HWP ──

    [Fact]
    public void Hwp_ToPlainText_Paragraphs()
    {
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph { Text = "안녕하세요" },
                        new HwpParagraph { Text = "반갑습니다" },
                    ],
                },
            ],
        };

        var text = content.ToPlainText();
        Assert.Contains("안녕하세요", text);
        Assert.Contains("반갑습니다", text);
    }

    [Fact]
    public void Hwp_ToPlainText_Tables()
    {
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Tables = [
                        new HwpTable
                        {
                            RowCount = 1, ColCount = 2,
                            Rows = [(IReadOnlyList<string>)["가", "나"]],
                        },
                    ],
                },
            ],
        };

        var text = content.ToPlainText();
        Assert.Contains("가", text);
        Assert.Contains("나", text);
    }

    [Fact]
    public void Hwp_ToMarkdown_BoldItalic()
    {
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph
                        {
                            Text = "Bold text",
                            Runs = [
                                new HwpRun { Text = "Bold", Bold = true },
                                new HwpRun { Text = " text" },
                            ],
                        },
                    ],
                },
            ],
        };

        var md = content.ToMarkdown();
        Assert.Contains("**Bold**", md);
    }

    [Fact]
    public void Hwp_ToMarkdown_MultipleSections()
    {
        var content = new HwpContent
        {
            Sections = [
                new HwpSection { Paragraphs = [new HwpParagraph { Text = "S1" }] },
                new HwpSection { Paragraphs = [new HwpParagraph { Text = "S2" }] },
            ],
        };

        var md = content.ToMarkdown();
        Assert.Contains("## Section 1", md);
        Assert.Contains("## Section 2", md);
    }
}
