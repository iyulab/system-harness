using SystemHarness.Apps.Office;

namespace SystemHarness.Tests.Office;

[Trait("Category", "CI")]
public class HwpxReaderTests : IDisposable
{
    private readonly HwpxReader _reader = new();
    private readonly string _tempDir;

    public HwpxReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sh-hwpx-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private string TempPath(string name) => Path.Combine(_tempDir, name);

    // ── Round-Trip Tests ──

    [Fact]
    public async Task RoundTrip_SingleParagraph()
    {
        var path = TempPath("single.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph { Text = "안녕하세요" },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        Assert.Single(result.Sections);
        Assert.Single(result.Sections[0].Paragraphs);
        Assert.Equal("안녕하세요", result.Sections[0].Paragraphs[0].Text);
    }

    [Fact]
    public async Task RoundTrip_MultipleParagraphs()
    {
        var path = TempPath("multi-para.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph { Text = "First paragraph" },
                        new HwpParagraph { Text = "Second paragraph" },
                        new HwpParagraph { Text = "Third paragraph" },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        Assert.Equal(3, result.Sections[0].Paragraphs.Count);
        Assert.Equal("First paragraph", result.Sections[0].Paragraphs[0].Text);
        Assert.Equal("Second paragraph", result.Sections[0].Paragraphs[1].Text);
        Assert.Equal("Third paragraph", result.Sections[0].Paragraphs[2].Text);
    }

    [Fact]
    public async Task RoundTrip_FormattedRuns()
    {
        var path = TempPath("runs.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph
                        {
                            Text = "Hello World",
                            Runs = [
                                new HwpRun { Text = "Hello ", CharShapeId = 1 },
                                new HwpRun { Text = "World", CharShapeId = 2 },
                            ],
                        },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        var para = result.Sections[0].Paragraphs[0];
        Assert.Equal(2, para.Runs.Count);
        Assert.Equal("Hello ", para.Runs[0].Text);
        Assert.Equal("World", para.Runs[1].Text);
        Assert.Equal(1, para.Runs[0].CharShapeId);
        Assert.Equal(2, para.Runs[1].CharShapeId);
    }

    [Fact]
    public async Task RoundTrip_CharShapeFormatting()
    {
        var path = TempPath("charshape.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph
                        {
                            Text = "Bold and styled",
                            Runs = [
                                new HwpRun
                                {
                                    Text = "Bold",
                                    CharShapeId = 0,
                                    Bold = true,
                                    FontFamily = "맑은 고딕",
                                    FontSize = 12.0,
                                    Color = "FF0000",
                                },
                                new HwpRun
                                {
                                    Text = " and ",
                                    CharShapeId = 1,
                                },
                                new HwpRun
                                {
                                    Text = "styled",
                                    CharShapeId = 2,
                                    Italic = true,
                                    Underline = true,
                                    FontSize = 14.0,
                                },
                            ],
                        },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        var runs = result.Sections[0].Paragraphs[0].Runs;
        Assert.Equal(3, runs.Count);

        // Run 0: bold, font, size, color
        Assert.True(runs[0].Bold);
        Assert.False(runs[0].Italic);
        Assert.Equal("맑은 고딕", runs[0].FontFamily);
        Assert.Equal(12.0, runs[0].FontSize);
        Assert.Equal("FF0000", runs[0].Color);

        // Run 1: no formatting
        Assert.False(runs[1].Bold);
        Assert.False(runs[1].Italic);

        // Run 2: italic + underline + size
        Assert.True(runs[2].Italic);
        Assert.True(runs[2].Underline);
        Assert.Equal(14.0, runs[2].FontSize);
    }

    [Fact]
    public async Task RoundTrip_ParaShapeId()
    {
        var path = TempPath("parashape.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph { Text = "Styled", ParaShapeId = 5 },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        Assert.Equal(5, result.Sections[0].Paragraphs[0].ParaShapeId);
    }

    [Fact]
    public async Task RoundTrip_Table()
    {
        var path = TempPath("table.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Tables = [
                        new HwpTable
                        {
                            RowCount = 2,
                            ColCount = 2,
                            Rows = [
                                (IReadOnlyList<string>)["A1", "B1"],
                                (IReadOnlyList<string>)["A2", "B2"],
                            ],
                        },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        Assert.Single(result.Sections[0].Tables);
        var table = result.Sections[0].Tables[0];
        Assert.Equal(2, table.RowCount);
        Assert.Equal(2, table.ColCount);
        Assert.Equal("A1", table.Rows[0][0]);
        Assert.Equal("B2", table.Rows[1][1]);
    }

    [Fact]
    public async Task RoundTrip_Image()
    {
        var path = TempPath("image.hwpx");
        // Simple 1x1 PNG
        var pngData = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Images = [
                        new HwpImage
                        {
                            Data = pngData,
                            ContentType = "image/png",
                            BinDataId = "img0",
                        },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        Assert.Single(result.Sections[0].Images);
        var img = result.Sections[0].Images[0];
        Assert.Equal("image/png", img.ContentType);
        Assert.Equal(pngData.Length, img.Data.Length);
        Assert.Equal(pngData, img.Data);
    }

    [Fact]
    public async Task RoundTrip_MultipleSections()
    {
        var path = TempPath("multi-section.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [new HwpParagraph { Text = "Section 1" }],
                },
                new HwpSection
                {
                    Paragraphs = [new HwpParagraph { Text = "Section 2" }],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        Assert.Equal(2, result.Sections.Count);
        Assert.Equal("Section 1", result.Sections[0].Paragraphs[0].Text);
        Assert.Equal("Section 2", result.Sections[1].Paragraphs[0].Text);
    }

    [Fact]
    public async Task RoundTrip_Mixed_ParagraphsTablesImages()
    {
        var path = TempPath("mixed.hwpx");
        var jpgData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 }; // fake JPEG header

        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph { Text = "Intro paragraph" },
                        new HwpParagraph { Text = "Closing paragraph" },
                    ],
                    Tables = [
                        new HwpTable
                        {
                            RowCount = 1,
                            ColCount = 2,
                            Rows = [(IReadOnlyList<string>)["Col1", "Col2"]],
                        },
                    ],
                    Images = [
                        new HwpImage
                        {
                            Data = jpgData,
                            ContentType = "image/jpeg",
                            BinDataId = "pic1",
                        },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        var section = result.Sections[0];
        Assert.Equal(2, section.Paragraphs.Count);
        Assert.Single(section.Tables);
        Assert.Single(section.Images);
        Assert.Equal("image/jpeg", section.Images[0].ContentType);
    }

    [Fact]
    public async Task RoundTrip_EmptyDocument()
    {
        var path = TempPath("empty.hwpx");
        var content = new HwpContent
        {
            Sections = [new HwpSection()],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        Assert.Single(result.Sections);
        Assert.Empty(result.Sections[0].Paragraphs);
        Assert.Empty(result.Sections[0].Tables);
        Assert.Empty(result.Sections[0].Images);
    }

    [Fact]
    public async Task RoundTrip_KoreanText()
    {
        var path = TempPath("korean.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph { Text = "한글 워드프로세서 테스트입니다." },
                        new HwpParagraph { Text = "가나다라마바사아자차카타파하" },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);
        var result = await _reader.ReadHwpxAsync(path);

        Assert.Equal("한글 워드프로세서 테스트입니다.", result.Sections[0].Paragraphs[0].Text);
        Assert.Equal("가나다라마바사아자차카타파하", result.Sections[0].Paragraphs[1].Text);
    }

    // ── Write Validation Tests ──

    [Fact]
    public async Task Write_CreatesValidZipStructure()
    {
        var path = TempPath("structure.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [new HwpParagraph { Text = "Test" }],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);

        // Verify ZIP structure
        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
        var entryNames = zip.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("mimetype", entryNames);
        Assert.Contains("META-INF/container.xml", entryNames);
        Assert.Contains("Contents/content.hpf", entryNames);
        Assert.Contains("Contents/header.xml", entryNames);
        Assert.Contains("Contents/section0.xml", entryNames);
    }

    [Fact]
    public async Task Write_MimetypeIsCorrect()
    {
        var path = TempPath("mimetype.hwpx");
        var content = new HwpContent
        {
            Sections = [new HwpSection()],
        };

        await _reader.WriteHwpxAsync(path, content);

        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
        var mimetypeEntry = zip.GetEntry("mimetype");
        Assert.NotNull(mimetypeEntry);

        using var reader = new StreamReader(mimetypeEntry!.Open());
        var mime = reader.ReadToEnd();
        Assert.Equal("application/hwp+zip", mime);
    }

    [Fact]
    public async Task Write_OverwritesExistingFile()
    {
        var path = TempPath("overwrite.hwpx");

        // Write first version
        var content1 = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [new HwpParagraph { Text = "Version 1" }],
                },
            ],
        };
        await _reader.WriteHwpxAsync(path, content1);

        // Write second version
        var content2 = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [new HwpParagraph { Text = "Version 2" }],
                },
            ],
        };
        await _reader.WriteHwpxAsync(path, content2);

        var result = await _reader.ReadHwpxAsync(path);
        Assert.Equal("Version 2", result.Sections[0].Paragraphs[0].Text);
    }

    [Fact]
    public async Task Write_ImageStoredInBinData()
    {
        var path = TempPath("bindata.hwpx");
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic

        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Images = [
                        new HwpImage
                        {
                            Data = pngData,
                            ContentType = "image/png",
                            BinDataId = "testimg",
                        },
                    ],
                },
            ],
        };

        await _reader.WriteHwpxAsync(path, content);

        using var zip = System.IO.Compression.ZipFile.OpenRead(path);
        var binEntries = zip.Entries.Where(e => e.FullName.StartsWith("BinData/", StringComparison.Ordinal)).ToList();
        Assert.Single(binEntries);
        Assert.EndsWith(".png", binEntries[0].Name);
    }

    // ── FindReplace Tests ──

    [Fact]
    public async Task FindReplace_BasicText()
    {
        var path = TempPath("findreplace.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph { Text = "Hello World, Hello Again" },
                    ],
                },
            ],
        };
        await _reader.WriteHwpxAsync(path, content);

        int count = await _reader.FindReplaceAsync(path, "Hello", "Hi");

        Assert.Equal(2, count);
        var result = await _reader.ReadHwpxAsync(path);
        Assert.Contains("Hi", result.Sections[0].Paragraphs[0].Text);
        Assert.DoesNotContain("Hello", result.Sections[0].Paragraphs[0].Text);
    }

    [Fact]
    public async Task FindReplace_InRuns()
    {
        var path = TempPath("findreplace-runs.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph
                        {
                            Text = "Test text here",
                            Runs = [
                                new HwpRun { Text = "Test ", Bold = true, CharShapeId = 0 },
                                new HwpRun { Text = "text here", CharShapeId = 1 },
                            ],
                        },
                    ],
                },
            ],
        };
        await _reader.WriteHwpxAsync(path, content);

        int count = await _reader.FindReplaceAsync(path, "text", "value");

        Assert.Equal(1, count);
        var result = await _reader.ReadHwpxAsync(path);
        // Bold run should be preserved
        Assert.True(result.Sections[0].Paragraphs[0].Runs[0].Bold);
        Assert.Contains("value", result.Sections[0].Paragraphs[0].Text);
    }

    [Fact]
    public async Task FindReplace_InTableCells()
    {
        var path = TempPath("findreplace-table.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Tables = [
                        new HwpTable
                        {
                            RowCount = 1, ColCount = 2,
                            Rows = [(IReadOnlyList<string>)["OLD value", "keep"]],
                        },
                    ],
                },
            ],
        };
        await _reader.WriteHwpxAsync(path, content);

        int count = await _reader.FindReplaceAsync(path, "OLD", "NEW");

        Assert.Equal(1, count);
        var result = await _reader.ReadHwpxAsync(path);
        Assert.Equal("NEW value", result.Sections[0].Tables[0].Rows[0][0]);
        Assert.Equal("keep", result.Sections[0].Tables[0].Rows[0][1]);
    }

    [Fact]
    public async Task FindReplace_NoMatch_ReturnsZero()
    {
        var path = TempPath("findreplace-nomatch.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [new HwpParagraph { Text = "Hello" }],
                },
            ],
        };
        await _reader.WriteHwpxAsync(path, content);

        int count = await _reader.FindReplaceAsync(path, "Goodbye", "Hi");

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task FindReplace_Korean()
    {
        var path = TempPath("findreplace-kr.hwpx");
        var content = new HwpContent
        {
            Sections = [
                new HwpSection
                {
                    Paragraphs = [
                        new HwpParagraph { Text = "한글 문서를 작성합니다" },
                    ],
                },
            ],
        };
        await _reader.WriteHwpxAsync(path, content);

        int count = await _reader.FindReplaceAsync(path, "한글", "한컴");

        Assert.Equal(1, count);
        var result = await _reader.ReadHwpxAsync(path);
        Assert.Equal("한컴 문서를 작성합니다", result.Sections[0].Paragraphs[0].Text);
    }
}
