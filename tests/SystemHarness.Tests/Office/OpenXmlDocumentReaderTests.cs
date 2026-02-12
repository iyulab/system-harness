using SystemHarness.Apps.Office;

namespace SystemHarness.Tests.Office;

[Trait("Category", "CI")]
public class OpenXmlDocumentReaderTests : IDisposable
{
    private readonly OpenXmlDocumentReader _reader = new();
    private readonly string _tempDir;

    public OpenXmlDocumentReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sh-office-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string TempPath(string name) => Path.Combine(_tempDir, name);

    // ── Word Tests ──

    [Fact]
    public async Task Word_RoundTrip_PlainText()
    {
        var path = TempPath("test.docx");
        var content = new DocumentContent
        {
            Paragraphs = [
                new DocumentParagraph { Text = "Hello World" },
                new DocumentParagraph { Text = "Second paragraph", Style = "Heading1" },
            ],
        };

        await _reader.WriteWordAsync(path, content);
        var result = await _reader.ReadWordAsync(path);

        Assert.Equal(2, result.Paragraphs.Count);
        Assert.Equal("Hello World", result.Paragraphs[0].Text);
        Assert.Equal("Heading1", result.Paragraphs[1].Style);
    }

    [Fact]
    public async Task Word_RoundTrip_FormattedRuns()
    {
        var path = TempPath("formatted.docx");
        var content = new DocumentContent
        {
            Paragraphs = [
                new DocumentParagraph
                {
                    Text = "Bold and italic",
                    Runs = [
                        new DocumentRun { Text = "Bold", Bold = true },
                        new DocumentRun { Text = " and ", },
                        new DocumentRun { Text = "italic", Italic = true },
                    ],
                },
            ],
        };

        await _reader.WriteWordAsync(path, content);
        var result = await _reader.ReadWordAsync(path);

        Assert.Single(result.Paragraphs);
        Assert.Equal(3, result.Paragraphs[0].Runs.Count);
        Assert.True(result.Paragraphs[0].Runs[0].Bold);
        Assert.True(result.Paragraphs[0].Runs[2].Italic);
    }

    [Fact]
    public async Task Word_RoundTrip_Tables()
    {
        var path = TempPath("tables.docx");
        var content = new DocumentContent
        {
            Tables = [
                new DocumentTable
                {
                    Rows = [
                        (IReadOnlyList<string>)["A1", "B1"],
                        (IReadOnlyList<string>)["A2", "B2"],
                    ],
                },
            ],
        };

        await _reader.WriteWordAsync(path, content);
        var result = await _reader.ReadWordAsync(path);

        Assert.Single(result.Tables);
        Assert.Equal(2, result.Tables[0].Rows.Count);
        Assert.Equal("A1", result.Tables[0].Rows[0][0]);
        Assert.Equal("B2", result.Tables[0].Rows[1][1]);
    }

    [Fact]
    public async Task Word_FindReplace()
    {
        var path = TempPath("findreplace.docx");
        var content = new DocumentContent
        {
            Paragraphs = [
                new DocumentParagraph { Text = "Hello World, Hello Again" },
            ],
        };
        await _reader.WriteWordAsync(path, content);

        int count = await _reader.FindReplaceWordAsync(path, "Hello", "Hi");

        Assert.Equal(2, count);

        var result = await _reader.ReadWordAsync(path);
        Assert.Contains("Hi", result.Paragraphs[0].Text);
        Assert.DoesNotContain("Hello", result.Paragraphs[0].Text);
    }

    [Fact]
    public async Task Word_HeaderFooter_RoundTrip()
    {
        var path = TempPath("headerfooter.docx");
        var content = new DocumentContent
        {
            Paragraphs = [new DocumentParagraph { Text = "Body" }],
            HeaderText = "My Header",
            FooterText = "Page 1",
        };

        await _reader.WriteWordAsync(path, content);
        var result = await _reader.ReadWordAsync(path);

        Assert.Equal("My Header", result.HeaderText);
        Assert.Equal("Page 1", result.FooterText);
    }

    // ── Excel Tests ──

    [Fact]
    public async Task Excel_RoundTrip_SimpleRows()
    {
        var path = TempPath("test.xlsx");
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet
                {
                    Name = "Data",
                    Rows = [
                        (IReadOnlyList<string>)["Name", "Value"],
                        (IReadOnlyList<string>)["Alice", "100"],
                    ],
                },
            ],
        };

        await _reader.WriteExcelAsync(path, content);
        var result = await _reader.ReadExcelAsync(path);

        Assert.Single(result.Sheets);
        Assert.Equal("Data", result.Sheets[0].Name);
        Assert.Equal(2, result.Sheets[0].Rows.Count);
        Assert.Equal("Name", result.Sheets[0].Rows[0][0]);
        Assert.Equal("100", result.Sheets[0].Rows[1][1]);
    }

    [Fact]
    public async Task Excel_RichRows_Populated()
    {
        var path = TempPath("rich.xlsx");
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet
                {
                    Name = "Sheet1",
                    Rows = [
                        (IReadOnlyList<string>)["Hello", "42"],
                    ],
                },
            ],
        };

        await _reader.WriteExcelAsync(path, content);
        var result = await _reader.ReadExcelAsync(path);

        Assert.NotEmpty(result.Sheets[0].RichRows);
        Assert.NotEmpty(result.Sheets[0].RichRows[0].Cells);
    }

    [Fact]
    public async Task Excel_MultipleSheets()
    {
        var path = TempPath("multi.xlsx");
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet { Name = "Sheet1", Rows = [(IReadOnlyList<string>)["A"]] },
                new SpreadsheetSheet { Name = "Sheet2", Rows = [(IReadOnlyList<string>)["B"]] },
            ],
        };

        await _reader.WriteExcelAsync(path, content);
        var result = await _reader.ReadExcelAsync(path);

        Assert.Equal(2, result.Sheets.Count);
        Assert.Equal("Sheet1", result.Sheets[0].Name);
        Assert.Equal("Sheet2", result.Sheets[1].Name);
    }

    // ── PowerPoint Tests ──

    [Fact]
    public async Task PowerPoint_RoundTrip_SimpleSlides()
    {
        var path = TempPath("test.pptx");
        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide { Number = 1, Texts = ["Title", "Subtitle"] },
                new PresentationSlide { Number = 2, Texts = ["Slide 2 Content"] },
            ],
        };

        await _reader.WritePowerPointAsync(path, content);
        var result = await _reader.ReadPowerPointAsync(path);

        Assert.Equal(2, result.Slides.Count);
        Assert.Contains("Title", result.Slides[0].Texts);
    }

    [Fact]
    public async Task PowerPoint_Shapes_Populated()
    {
        var path = TempPath("shapes.pptx");
        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide { Number = 1, Texts = ["Hello from shape"] },
            ],
        };

        await _reader.WritePowerPointAsync(path, content);
        var result = await _reader.ReadPowerPointAsync(path);

        // The writer creates shapes from texts, so shapes should be populated
        Assert.NotEmpty(result.Slides[0].Shapes);
    }

    [Fact]
    public async Task PowerPoint_Write_ShapesWithPosition()
    {
        var path = TempPath("positioned.pptx");
        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide
                {
                    Number = 1,
                    Shapes = [
                        new PresentationShape
                        {
                            Name = "Title",
                            Type = PresentationShapeType.TextBox,
                            X = 914400, Y = 274638,
                            Width = 6858000, Height = 1143000,
                            Text = "My Title",
                        },
                        new PresentationShape
                        {
                            Name = "Body",
                            Type = PresentationShapeType.TextBox,
                            X = 914400, Y = 1600200,
                            Width = 6858000, Height = 3200400,
                            Text = "Body content",
                        },
                    ],
                },
            ],
        };

        await _reader.WritePowerPointAsync(path, content);
        var result = await _reader.ReadPowerPointAsync(path);

        Assert.Single(result.Slides);
        Assert.Equal(2, result.Slides[0].Shapes.Count);
        Assert.Equal("Title", result.Slides[0].Shapes[0].Name);
        Assert.Equal("Body", result.Slides[0].Shapes[1].Name);
        Assert.Equal(914400, result.Slides[0].Shapes[0].X);
    }

    [Fact]
    public async Task PowerPoint_Write_FormattedTextRuns()
    {
        var path = TempPath("formatted-pptx.pptx");
        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide
                {
                    Number = 1,
                    Shapes = [
                        new PresentationShape
                        {
                            Name = "Formatted",
                            Type = PresentationShapeType.TextBox,
                            TextRuns = [
                                new DocumentRun { Text = "Bold", Bold = true },
                                new DocumentRun { Text = " Normal " },
                                new DocumentRun { Text = "Italic", Italic = true },
                            ],
                        },
                    ],
                },
            ],
        };

        await _reader.WritePowerPointAsync(path, content);
        var result = await _reader.ReadPowerPointAsync(path);

        var runs = result.Slides[0].Shapes[0].TextRuns;
        Assert.Equal(3, runs.Count);
        Assert.True(runs[0].Bold);
        Assert.True(runs[2].Italic);
    }

    [Fact]
    public async Task PowerPoint_Write_Notes()
    {
        var path = TempPath("notes.pptx");
        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide
                {
                    Number = 1,
                    Texts = ["Slide with notes"],
                    Notes = "Speaker notes here",
                },
            ],
        };

        await _reader.WritePowerPointAsync(path, content);
        var result = await _reader.ReadPowerPointAsync(path);

        Assert.NotNull(result.Slides[0].Notes);
        Assert.Contains("Speaker notes here", result.Slides[0].Notes);
    }

    [Fact]
    public async Task PowerPoint_Write_Images()
    {
        var path = TempPath("pptx-images.pptx");
        // Create a minimal 1x1 PNG
        var png = new byte[] {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x00, 0x02, 0x00, 0x01, 0xE2, 0x21, 0xBC,
            0x33, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82,
        };

        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide
                {
                    Number = 1,
                    Texts = ["Slide with image"],
                    Images = [
                        new PresentationImage { Data = png, ContentType = "image/png", Description = "Test Image" },
                    ],
                },
            ],
        };

        await _reader.WritePowerPointAsync(path, content);
        var result = await _reader.ReadPowerPointAsync(path);

        Assert.NotEmpty(result.Slides[0].Images);
        Assert.Equal("image/png", result.Slides[0].Images[0].ContentType);
    }

    [Fact]
    public async Task PowerPoint_Write_MultipleSlides_MixedContent()
    {
        var path = TempPath("mixed-pptx.pptx");
        var content = new PresentationContent
        {
            Slides = [
                new PresentationSlide
                {
                    Number = 1,
                    Shapes = [
                        new PresentationShape { Name = "Title", Text = "First Slide" },
                    ],
                    Notes = "Notes for slide 1",
                },
                new PresentationSlide
                {
                    Number = 2,
                    Texts = ["Second Slide"],
                },
                new PresentationSlide
                {
                    Number = 3,
                    Shapes = [
                        new PresentationShape { Name = "Shape1", Text = "A" },
                        new PresentationShape { Name = "Shape2", Text = "B" },
                    ],
                },
            ],
        };

        await _reader.WritePowerPointAsync(path, content);
        var result = await _reader.ReadPowerPointAsync(path);

        Assert.Equal(3, result.Slides.Count);
        Assert.NotNull(result.Slides[0].Notes);
        Assert.Contains("Second Slide", result.Slides[1].Texts);
        Assert.Equal(2, result.Slides[2].Shapes.Count);
    }

    // ── Excel RichRows Write Tests ──

    [Fact]
    public async Task Excel_Write_TypedCells_Number()
    {
        var path = TempPath("typed-number.xlsx");
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet
                {
                    Name = "Data",
                    RichRows = [
                        new SpreadsheetRow
                        {
                            RowIndex = 1,
                            Cells = [
                                new SpreadsheetCell { Address = "A1", Value = "Name", Type = CellValueType.String },
                                new SpreadsheetCell { Address = "B1", Value = "42", Type = CellValueType.Number },
                            ],
                        },
                    ],
                },
            ],
        };

        await _reader.WriteExcelAsync(path, content);
        var result = await _reader.ReadExcelAsync(path);

        Assert.Single(result.Sheets);
        Assert.NotEmpty(result.Sheets[0].RichRows);
        var cells = result.Sheets[0].RichRows[0].Cells;
        Assert.Equal("Name", cells.First(c => c.Address == "A1").Value);
        Assert.Equal("42", cells.First(c => c.Address == "B1").Value);
    }

    [Fact]
    public async Task Excel_Write_Formula()
    {
        var path = TempPath("formula.xlsx");
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet
                {
                    Name = "Calc",
                    RichRows = [
                        new SpreadsheetRow
                        {
                            RowIndex = 1,
                            Cells = [
                                new SpreadsheetCell { Address = "A1", Value = "10", Type = CellValueType.Number },
                                new SpreadsheetCell { Address = "B1", Value = "20", Type = CellValueType.Number },
                                new SpreadsheetCell { Address = "C1", Formula = "A1+B1", Type = CellValueType.Formula },
                            ],
                        },
                    ],
                },
            ],
        };

        await _reader.WriteExcelAsync(path, content);
        var result = await _reader.ReadExcelAsync(path);

        var cells = result.Sheets[0].RichRows[0].Cells;
        var formulaCell = cells.First(c => c.Address == "C1");
        Assert.Equal("A1+B1", formulaCell.Formula);
    }

    [Fact]
    public async Task Excel_Write_StyledCells()
    {
        var path = TempPath("styled.xlsx");
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet
                {
                    Name = "Styled",
                    RichRows = [
                        new SpreadsheetRow
                        {
                            RowIndex = 1,
                            Cells = [
                                new SpreadsheetCell
                                {
                                    Address = "A1",
                                    Value = "Bold",
                                    Type = CellValueType.String,
                                    Style = new CellStyle { Bold = true },
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        await _reader.WriteExcelAsync(path, content);
        var result = await _reader.ReadExcelAsync(path);

        var cell = result.Sheets[0].RichRows[0].Cells.First(c => c.Address == "A1");
        Assert.NotNull(cell.Style);
        Assert.True(cell.Style!.Bold);
    }

    [Fact]
    public async Task Excel_Write_MergedCells()
    {
        var path = TempPath("merged.xlsx");
        var content = new SpreadsheetContent
        {
            Sheets = [
                new SpreadsheetSheet
                {
                    Name = "Merged",
                    RichRows = [
                        new SpreadsheetRow
                        {
                            RowIndex = 1,
                            Cells = [
                                new SpreadsheetCell { Address = "A1", Value = "Merged", Type = CellValueType.String },
                            ],
                        },
                    ],
                    MergedCells = ["A1:C1"],
                },
            ],
        };

        await _reader.WriteExcelAsync(path, content);
        var result = await _reader.ReadExcelAsync(path);

        Assert.Contains("A1:C1", result.Sheets[0].MergedCells);
    }
}
