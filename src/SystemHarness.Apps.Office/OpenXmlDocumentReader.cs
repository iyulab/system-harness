using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SystemHarness.Apps.Office;

/// <summary>
/// OpenXML-based implementation of <see cref="IDocumentReader"/>.
/// No Office installation required.
/// </summary>
public sealed class OpenXmlDocumentReader : IDocumentReader
{
    public Task<DocumentContent> ReadWordAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var doc = WordprocessingDocument.Open(filePath, false);
            var mainPart = doc.MainDocumentPart;
            var body = mainPart?.Document?.Body;
            if (body is null || mainPart is null)
                return new DocumentContent();

            // Build hyperlink lookup: rId → URI
            var hyperlinkMap = mainPart.HyperlinkRelationships
                .ToDictionary(h => h.Id, h => h.Uri.ToString());

            // Parse numbering definitions for list detection
            var numberingPart = mainPart.NumberingDefinitionsPart;

            var paragraphs = new List<DocumentParagraph>();
            foreach (var p in body.Elements<W.Paragraph>())
            {
                ct.ThrowIfCancellationRequested();
                paragraphs.Add(ParseParagraph(p, hyperlinkMap, numberingPart));
            }

            var tables = body.Elements<W.Table>()
                .Select(ParseTable)
                .ToList();

            var images = ExtractImages(mainPart);

            // Extract header/footer text from first section
            string? headerText = null;
            string? footerText = null;

            var headerPart = mainPart.HeaderParts.FirstOrDefault();
            if (headerPart?.Header is not null)
                headerText = headerPart.Header.InnerText;

            var footerPart = mainPart.FooterParts.FirstOrDefault();
            if (footerPart?.Footer is not null)
                footerText = footerPart.Footer.InnerText;

            return new DocumentContent
            {
                Paragraphs = paragraphs,
                Tables = tables,
                Images = images,
                HeaderText = string.IsNullOrWhiteSpace(headerText) ? null : headerText,
                FooterText = string.IsNullOrWhiteSpace(footerText) ? null : footerText,
            };
        }, ct);
    }

    public Task<int> FindReplaceWordAsync(string filePath, string find, string replace, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var doc = WordprocessingDocument.Open(filePath, true);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null) return 0;

            int count = 0;
            foreach (var text in body.Descendants<W.Text>())
            {
                ct.ThrowIfCancellationRequested();
                if (text.Text.Contains(find, StringComparison.Ordinal))
                {
                    int occurrences = CountOccurrences(text.Text, find);
                    text.Text = text.Text.Replace(find, replace, StringComparison.Ordinal);
                    count += occurrences;
                }
            }

            return count;
        }, ct);
    }

    public Task WriteWordAsync(string filePath, DocumentContent content, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var doc = WordprocessingDocument.Create(filePath,
                DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new W.Document(new W.Body());

            // Create numbering definitions if any paragraph uses lists
            if (content.Paragraphs.Any(p => p.ListType is not null))
            {
                EnsureNumberingPart(mainPart);
            }

            foreach (var para in content.Paragraphs)
            {
                ct.ThrowIfCancellationRequested();
                var p = BuildParagraph(para, mainPart);
                mainPart.Document.Body!.Append(p);
            }

            foreach (var table in content.Tables)
            {
                var t = new W.Table();
                foreach (var row in table.Rows)
                {
                    var tr = new W.TableRow();
                    foreach (var cell in row)
                    {
                        tr.Append(new W.TableCell(new W.Paragraph(new W.Run(new W.Text(cell)))));
                    }
                    t.Append(tr);
                }
                mainPart.Document.Body!.Append(t);
            }

            // Write images as inline drawings
            foreach (var image in content.Images)
            {
                WriteInlineImage(mainPart, image);
            }

            // Write header
            if (content.HeaderText is not null)
            {
                var headerPart = mainPart.AddNewPart<HeaderPart>();
                headerPart.Header = new W.Header(
                    new W.Paragraph(new W.Run(new W.Text(content.HeaderText))));
                var headerRef = new W.HeaderReference
                {
                    Type = W.HeaderFooterValues.Default,
                    Id = mainPart.GetIdOfPart(headerPart),
                };
                EnsureSectionProperties(mainPart).Append(headerRef);
            }

            // Write footer
            if (content.FooterText is not null)
            {
                var footerPart = mainPart.AddNewPart<FooterPart>();
                footerPart.Footer = new W.Footer(
                    new W.Paragraph(new W.Run(new W.Text(content.FooterText))));
                var footerRef = new W.FooterReference
                {
                    Type = W.HeaderFooterValues.Default,
                    Id = mainPart.GetIdOfPart(footerPart),
                };
                EnsureSectionProperties(mainPart).Append(footerRef);
            }
        }, ct);
    }

    public Task<SpreadsheetContent> ReadExcelAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var doc = SpreadsheetDocument.Open(filePath, false);
            var workbookPart = doc.WorkbookPart;
            if (workbookPart?.Workbook is null)
                return new SpreadsheetContent();

            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
            var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;

            var sheets = workbookPart.Workbook.Sheets?.Elements<S.Sheet>() ?? [];
            var result = new List<SpreadsheetSheet>();

            foreach (var sheet in sheets)
            {
                ct.ThrowIfCancellationRequested();
                if (sheet.Id is null) continue;
                var worksheetPart = (WorksheetPart?)workbookPart.GetPartById(sheet.Id!);
                if (worksheetPart?.Worksheet is null) continue;

                var simpleRows = new List<IReadOnlyList<string>>();
                var richRows = new List<SpreadsheetRow>();

                foreach (var row in worksheetPart.Worksheet.Elements<S.SheetData>()
                    .SelectMany(sd => sd.Elements<S.Row>()))
                {
                    var simpleCells = new List<string>();
                    var richCells = new List<SpreadsheetCell>();

                    foreach (var cell in row.Elements<S.Cell>())
                    {
                        var displayValue = GetCellValue(cell, sharedStrings);
                        simpleCells.Add(displayValue);
                        richCells.Add(ParseExcelCell(cell, displayValue, sharedStrings, stylesheet));
                    }

                    simpleRows.Add(simpleCells);
                    richRows.Add(new SpreadsheetRow
                    {
                        RowIndex = (int)(row.RowIndex?.Value ?? 0),
                        Cells = richCells,
                    });
                }

                // Extract merged cells
                var mergedCells = worksheetPart.Worksheet.Elements<S.MergeCells>()
                    .SelectMany(mc => mc.Elements<S.MergeCell>())
                    .Select(mc => mc.Reference?.Value ?? "")
                    .Where(r => r.Length > 0)
                    .ToList();

                result.Add(new SpreadsheetSheet
                {
                    Name = sheet.Name?.Value ?? "Sheet",
                    Rows = simpleRows,
                    RichRows = richRows,
                    MergedCells = mergedCells,
                });
            }

            return new SpreadsheetContent { Sheets = result };
        }, ct);
    }

    public Task WriteExcelAsync(string filePath, SpreadsheetContent content, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var doc = SpreadsheetDocument.Create(filePath,
                DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);

            var workbookPart = doc.AddWorkbookPart();
            workbookPart.Workbook = new S.Workbook(new S.Sheets());

            // Build stylesheet for cell formatting
            var (stylesheet, styleMap) = BuildStylesheet(content);
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = stylesheet;

            uint sheetId = 1;
            foreach (var sheet in content.Sheets)
            {
                ct.ThrowIfCancellationRequested();
                var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                var sheetData = new S.SheetData();

                if (sheet.RichRows.Count > 0)
                {
                    // Write from RichRows (typed cells with styles)
                    foreach (var richRow in sheet.RichRows)
                    {
                        var r = new S.Row { RowIndex = (uint)richRow.RowIndex };
                        foreach (var cell in richRow.Cells)
                        {
                            r.Append(BuildExcelCell(cell, styleMap));
                        }
                        sheetData.Append(r);
                    }
                }
                else
                {
                    // Fallback: write from simple string Rows
                    for (int ri = 0; ri < sheet.Rows.Count; ri++)
                    {
                        var row = sheet.Rows[ri];
                        var r = new S.Row { RowIndex = (uint)(ri + 1) };
                        for (int ci = 0; ci < row.Count; ci++)
                        {
                            r.Append(new S.Cell
                            {
                                CellReference = CellAddress.FromIndices(ri, ci),
                                DataType = S.CellValues.String,
                                CellValue = new S.CellValue(row[ci]),
                            });
                        }
                        sheetData.Append(r);
                    }
                }

                worksheetPart.Worksheet = new S.Worksheet(sheetData);

                // Write merged cells
                if (sheet.MergedCells.Count > 0)
                {
                    var mergeCells = new S.MergeCells();
                    foreach (var range in sheet.MergedCells)
                    {
                        mergeCells.Append(new S.MergeCell { Reference = range });
                    }
                    worksheetPart.Worksheet.Append(mergeCells);
                }

                workbookPart.Workbook.Sheets!.Append(new S.Sheet
                {
                    Id = workbookPart.GetIdOfPart(worksheetPart),
                    SheetId = sheetId++,
                    Name = sheet.Name,
                });
            }
        }, ct);
    }

    public Task<PresentationContent> ReadPowerPointAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var doc = PresentationDocument.Open(filePath, false);
            var presentationPart = doc.PresentationPart;
            if (presentationPart?.Presentation is null)
                return new PresentationContent();

            var slideIds = presentationPart.Presentation.SlideIdList?
                .Elements<DocumentFormat.OpenXml.Presentation.SlideId>() ?? [];

            var slides = new List<PresentationSlide>();
            int number = 1;

            foreach (var slideId in slideIds)
            {
                ct.ThrowIfCancellationRequested();
                if (slideId.RelationshipId is null) continue;
                var slidePart = (SlidePart?)presentationPart.GetPartById(slideId.RelationshipId!);
                if (slidePart?.Slide is null) continue;

                // Backward-compatible text extraction
                var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>()
                    .Select(t => t.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                // Extract shapes
                var shapes = new List<PresentationShape>();
                var slideTree = slidePart.Slide.CommonSlideData?.ShapeTree;
                if (slideTree is not null)
                {
                    foreach (var shape in slideTree.Elements<DocumentFormat.OpenXml.Presentation.Shape>())
                    {
                        shapes.Add(ParsePptShape(shape));
                    }
                }

                // Extract images
                var images = new List<PresentationImage>();
                foreach (var imagePart in slidePart.ImageParts)
                {
                    using var stream = imagePart.GetStream();
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    var data = ms.ToArray();
                    if (data.Length > 0)
                    {
                        images.Add(new PresentationImage
                        {
                            Data = data,
                            ContentType = imagePart.ContentType,
                        });
                    }
                }

                // Layout name
                string? layoutName = null;
                if (slidePart.SlideLayoutPart?.SlideLayout?.CommonSlideData is not null)
                    layoutName = slidePart.SlideLayoutPart.SlideLayout.CommonSlideData.Name;

                // Notes
                string? notes = null;
                if (slidePart.NotesSlidePart?.NotesSlide is not null)
                {
                    notes = string.Join(" ", slidePart.NotesSlidePart.NotesSlide
                        .Descendants<DocumentFormat.OpenXml.Drawing.Text>()
                        .Select(t => t.Text)
                        .Where(t => !string.IsNullOrWhiteSpace(t)));
                }

                slides.Add(new PresentationSlide
                {
                    Number = number++,
                    Texts = texts,
                    Shapes = shapes,
                    Images = images,
                    LayoutName = layoutName,
                    Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                });
            }

            return new PresentationContent { Slides = slides };
        }, ct);
    }

    public Task WritePowerPointAsync(string filePath, PresentationContent content, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var doc = PresentationDocument.Create(filePath,
                DocumentFormat.OpenXml.PresentationDocumentType.Presentation);

            var presentationPart = doc.AddPresentationPart();
            presentationPart.Presentation = new DocumentFormat.OpenXml.Presentation.Presentation(
                new DocumentFormat.OpenXml.Presentation.SlideIdList());

            uint slideIdVal = 256;
            foreach (var slide in content.Slides)
            {
                ct.ThrowIfCancellationRequested();
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                var shapeTree = new DocumentFormat.OpenXml.Presentation.ShapeTree();
                uint shapeId = 1;

                if (slide.Shapes.Count > 0)
                {
                    // Write from Shapes with positioning and formatting
                    foreach (var shape in slide.Shapes)
                    {
                        shapeTree.Append(BuildPptShape(shape, shapeId++));
                    }
                }
                else if (slide.Texts.Count > 0)
                {
                    // Fallback: create a text body shape from Texts
                    var bodyShape = new DocumentFormat.OpenXml.Presentation.Shape(
                        new DocumentFormat.OpenXml.Presentation.NonVisualShapeProperties(
                            new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = shapeId++, Name = "TextBox" },
                            new DocumentFormat.OpenXml.Presentation.NonVisualShapeDrawingProperties(),
                            new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                        new DocumentFormat.OpenXml.Presentation.ShapeProperties(
                            new DocumentFormat.OpenXml.Drawing.Transform2D(
                                new DocumentFormat.OpenXml.Drawing.Offset { X = 457200, Y = 274638 },
                                new DocumentFormat.OpenXml.Drawing.Extents { Cx = 8229600, Cy = 5851525 }),
                            new DocumentFormat.OpenXml.Drawing.PresetGeometry { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }),
                        BuildTextBodyFromTexts(slide.Texts));
                    shapeTree.Append(bodyShape);
                }

                // Write images as picture shapes
                foreach (var image in slide.Images)
                {
                    var imagePart = slidePart.AddImagePart(
                        image.ContentType switch
                        {
                            "image/png" => ImagePartType.Png,
                            "image/gif" => ImagePartType.Gif,
                            "image/bmp" => ImagePartType.Bmp,
                            _ => ImagePartType.Jpeg,
                        });
                    using (var ms = new MemoryStream(image.Data))
                        imagePart.FeedData(ms);

                    var rId = slidePart.GetIdOfPart(imagePart);
                    long cx = 914400L * 4; // 4 inches
                    long cy = 914400L * 3; // 3 inches

                    var picShape = new DocumentFormat.OpenXml.Presentation.Picture(
                        new DocumentFormat.OpenXml.Presentation.NonVisualPictureProperties(
                            new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties
                            {
                                Id = shapeId++,
                                Name = image.Description ?? "Image",
                            },
                            new DocumentFormat.OpenXml.Presentation.NonVisualPictureDrawingProperties(
                                new DocumentFormat.OpenXml.Drawing.PictureLocks { NoChangeAspect = true }),
                            new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                        new DocumentFormat.OpenXml.Presentation.BlipFill(
                            new DocumentFormat.OpenXml.Drawing.Blip { Embed = rId },
                            new DocumentFormat.OpenXml.Drawing.Stretch(
                                new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                        new DocumentFormat.OpenXml.Presentation.ShapeProperties(
                            new DocumentFormat.OpenXml.Drawing.Transform2D(
                                new DocumentFormat.OpenXml.Drawing.Offset { X = 457200, Y = 274638 },
                                new DocumentFormat.OpenXml.Drawing.Extents { Cx = cx, Cy = cy }),
                            new DocumentFormat.OpenXml.Drawing.PresetGeometry { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }));

                    shapeTree.Append(picShape);
                }

                slidePart.Slide = new DocumentFormat.OpenXml.Presentation.Slide(
                    new DocumentFormat.OpenXml.Presentation.CommonSlideData(shapeTree));

                // Write notes
                if (slide.Notes is not null)
                {
                    var notesSlidePart = slidePart.AddNewPart<NotesSlidePart>();
                    notesSlidePart.NotesSlide = new DocumentFormat.OpenXml.Presentation.NotesSlide(
                        new DocumentFormat.OpenXml.Presentation.CommonSlideData(
                            new DocumentFormat.OpenXml.Presentation.ShapeTree(
                                new DocumentFormat.OpenXml.Presentation.Shape(
                                    new DocumentFormat.OpenXml.Presentation.NonVisualShapeProperties(
                                        new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = 1, Name = "Notes" },
                                        new DocumentFormat.OpenXml.Presentation.NonVisualShapeDrawingProperties(),
                                        new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
                                    new DocumentFormat.OpenXml.Presentation.ShapeProperties(),
                                    new DocumentFormat.OpenXml.Presentation.TextBody(
                                        new DocumentFormat.OpenXml.Drawing.BodyProperties(),
                                        new DocumentFormat.OpenXml.Drawing.Paragraph(
                                            new DocumentFormat.OpenXml.Drawing.Run(
                                                new DocumentFormat.OpenXml.Drawing.Text(slide.Notes))))))));
                }

                presentationPart.Presentation.SlideIdList!.Append(
                    new DocumentFormat.OpenXml.Presentation.SlideId
                    {
                        Id = slideIdVal++,
                        RelationshipId = presentationPart.GetIdOfPart(slidePart),
                    });
            }
        }, ct);
    }

    private static DocumentFormat.OpenXml.Presentation.TextBody BuildTextBodyFromTexts(IReadOnlyList<string> texts)
    {
        var tb = new DocumentFormat.OpenXml.Presentation.TextBody(
            new DocumentFormat.OpenXml.Drawing.BodyProperties());
        foreach (var text in texts)
        {
            tb.Append(new DocumentFormat.OpenXml.Drawing.Paragraph(
                new DocumentFormat.OpenXml.Drawing.Run(
                    new DocumentFormat.OpenXml.Drawing.Text(text))));
        }
        return tb;
    }

    private static DocumentFormat.OpenXml.Presentation.Shape BuildPptShape(PresentationShape shape, uint shapeId)
    {
        // Build text body from TextRuns or Text
        var textBody = new DocumentFormat.OpenXml.Presentation.TextBody(
            new DocumentFormat.OpenXml.Drawing.BodyProperties());

        if (shape.TextRuns.Count > 0)
        {
            var para = new DocumentFormat.OpenXml.Drawing.Paragraph();
            foreach (var run in shape.TextRuns)
            {
                var drawingRun = new DocumentFormat.OpenXml.Drawing.Run(
                    new DocumentFormat.OpenXml.Drawing.Text(run.Text));

                var rp = new DocumentFormat.OpenXml.Drawing.RunProperties();
                bool hasRunProps = false;

                if (run.Bold) { rp.Bold = true; hasRunProps = true; }
                if (run.Italic) { rp.Italic = true; hasRunProps = true; }
                if (run.Underline)
                {
                    rp.Underline = DocumentFormat.OpenXml.Drawing.TextUnderlineValues.Single;
                    hasRunProps = true;
                }
                if (run.FontFamily is not null)
                {
                    rp.Append(new DocumentFormat.OpenXml.Drawing.LatinFont { Typeface = run.FontFamily });
                    hasRunProps = true;
                }
                if (run.FontSize is double sz)
                {
                    rp.FontSize = (int)(sz * 100); // OpenXML uses hundredths of a point
                    hasRunProps = true;
                }

                if (hasRunProps)
                    drawingRun.RunProperties = rp;

                para.Append(drawingRun);
            }
            textBody.Append(para);
        }
        else if (shape.Text is not null)
        {
            textBody.Append(new DocumentFormat.OpenXml.Drawing.Paragraph(
                new DocumentFormat.OpenXml.Drawing.Run(
                    new DocumentFormat.OpenXml.Drawing.Text(shape.Text))));
        }

        // Build transform
        long x = shape.X ?? 457200;   // default ~0.5 inch
        long y = shape.Y ?? 274638;
        long w = shape.Width ?? 8229600;  // default ~9 inches
        long h = shape.Height ?? 914400;  // default 1 inch

        return new DocumentFormat.OpenXml.Presentation.Shape(
            new DocumentFormat.OpenXml.Presentation.NonVisualShapeProperties(
                new DocumentFormat.OpenXml.Presentation.NonVisualDrawingProperties { Id = shapeId, Name = shape.Name },
                new DocumentFormat.OpenXml.Presentation.NonVisualShapeDrawingProperties(),
                new DocumentFormat.OpenXml.Presentation.ApplicationNonVisualDrawingProperties()),
            new DocumentFormat.OpenXml.Presentation.ShapeProperties(
                new DocumentFormat.OpenXml.Drawing.Transform2D(
                    new DocumentFormat.OpenXml.Drawing.Offset { X = x, Y = y },
                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = w, Cy = h }),
                new DocumentFormat.OpenXml.Drawing.PresetGeometry { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }),
            textBody);
    }

    private static DocumentParagraph ParseParagraph(
        W.Paragraph p,
        Dictionary<string, string> hyperlinkMap,
        NumberingDefinitionsPart? numberingPart)
    {
        var runs = new List<DocumentRun>();

        foreach (var element in p.ChildElements)
        {
            if (element is W.Hyperlink hyperlink)
            {
                string? uri = null;
                if (hyperlink.Id?.Value is string rId && hyperlinkMap.TryGetValue(rId, out var u))
                    uri = u;

                foreach (var run in hyperlink.Elements<W.Run>())
                {
                    runs.Add(ParseRun(run, uri));
                }
            }
            else if (element is W.Run run)
            {
                runs.Add(ParseRun(run));
            }
        }

        // Detect list
        int? listLevel = null;
        ListType? listType = null;
        var numPr = p.ParagraphProperties?.NumberingProperties;
        if (numPr is not null)
        {
            listLevel = numPr.NumberingLevelReference?.Val?.Value ?? 0;
            listType = DetectListType(numPr, numberingPart);
        }

        return new DocumentParagraph
        {
            Text = p.InnerText,
            Style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value,
            Runs = runs,
            ListLevel = listLevel,
            ListType = listType,
        };
    }

    private static DocumentRun ParseRun(W.Run run, string? hyperlinkUri = null)
    {
        var rp = run.RunProperties;
        return new DocumentRun
        {
            Text = run.InnerText,
            Bold = rp?.Bold is not null && (rp.Bold.Val is null || rp.Bold.Val.Value),
            Italic = rp?.Italic is not null && (rp.Italic.Val is null || rp.Italic.Val.Value),
            Underline = rp?.Underline is not null && rp.Underline.Val is not null
                && rp.Underline.Val.Value != UnderlineValues.None,
            Strikethrough = rp?.Strike is not null && (rp.Strike.Val is null || rp.Strike.Val.Value),
            FontFamily = rp?.RunFonts?.Ascii?.Value,
            FontSize = rp?.FontSize?.Val?.Value is string sz && double.TryParse(sz, out var pts)
                ? pts / 2.0  // OpenXML stores font size in half-points
                : null,
            Color = rp?.Color?.Val?.Value,
            HyperlinkUri = hyperlinkUri,
        };
    }

    private static ListType? DetectListType(
        W.NumberingProperties numPr,
        NumberingDefinitionsPart? numberingPart)
    {
        if (numberingPart?.Numbering is null || numPr.NumberingId?.Val?.Value is not int numId)
            return Office.ListType.Bullet; // default

        var numInstance = numberingPart.Numbering.Elements<W.NumberingInstance>()
            .FirstOrDefault(ni => ni.NumberID?.Value == numId);
        if (numInstance?.AbstractNumId?.Val?.Value is not int abstractNumId)
            return Office.ListType.Bullet;

        var abstractNum = numberingPart.Numbering.Elements<W.AbstractNum>()
            .FirstOrDefault(an => an.AbstractNumberId?.Value == abstractNumId);

        var level = abstractNum?.Elements<W.Level>()
            .FirstOrDefault(l => l.LevelIndex?.Value == (numPr.NumberingLevelReference?.Val?.Value ?? 0));

        if (level?.NumberingFormat?.Val?.Value is NumberFormatValues fmt)
        {
            return fmt == NumberFormatValues.Bullet
                ? Office.ListType.Bullet
                : Office.ListType.Numbered;
        }

        return Office.ListType.Bullet;
    }

    private static DocumentTable ParseTable(W.Table t)
    {
        return new DocumentTable
        {
            Rows = t.Elements<W.TableRow>()
                .Select(r => (IReadOnlyList<string>)r.Elements<W.TableCell>()
                    .Select(c => c.InnerText)
                    .ToList())
                .ToList(),
        };
    }

    private static List<DocumentImage> ExtractImages(MainDocumentPart mainPart)
    {
        var images = new List<DocumentImage>();

        foreach (var imagePart in mainPart.ImageParts)
        {
            using var stream = imagePart.GetStream();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var data = ms.ToArray();
            if (data.Length == 0) continue;

            images.Add(new DocumentImage
            {
                Data = data,
                ContentType = imagePart.ContentType,
            });
        }

        return images;
    }

    private static W.Paragraph BuildParagraph(DocumentParagraph para, MainDocumentPart mainPart)
    {
        var p = new W.Paragraph();

        // Build paragraph properties (style + optional list numbering)
        W.ParagraphProperties? pPr = null;

        if (para.Style is not null)
        {
            pPr = new W.ParagraphProperties(new W.ParagraphStyleId { Val = para.Style });
        }

        if (para.ListType is not null)
        {
            pPr ??= new W.ParagraphProperties();
            int numId = para.ListType == ListType.Bullet ? 1 : 2;
            int level = para.ListLevel ?? 0;
            pPr.Append(new W.NumberingProperties(
                new W.NumberingLevelReference { Val = level },
                new W.NumberingId { Val = numId }));
        }

        if (pPr is not null)
            p.ParagraphProperties = pPr;

        // If runs are provided, use them for rich formatting
        if (para.Runs.Count > 0)
        {
            foreach (var docRun in para.Runs)
            {
                if (docRun.HyperlinkUri is not null)
                {
                    var rel = mainPart.AddHyperlinkRelationship(new Uri(docRun.HyperlinkUri), true);
                    var hyperlink = new W.Hyperlink { Id = rel.Id };
                    hyperlink.Append(BuildRun(docRun));
                    p.Append(hyperlink);
                }
                else
                {
                    p.Append(BuildRun(docRun));
                }
            }
        }
        else
        {
            // Fallback: plain text
            p.Append(new W.Run(new W.Text(para.Text) { Space = SpaceProcessingModeValues.Preserve }));
        }

        return p;
    }

    private static W.Run BuildRun(DocumentRun docRun)
    {
        var run = new W.Run();
        var rp = new W.RunProperties();
        bool hasProps = false;

        if (docRun.Bold) { rp.Append(new W.Bold()); hasProps = true; }
        if (docRun.Italic) { rp.Append(new W.Italic()); hasProps = true; }
        if (docRun.Underline) { rp.Append(new W.Underline { Val = UnderlineValues.Single }); hasProps = true; }
        if (docRun.Strikethrough) { rp.Append(new W.Strike()); hasProps = true; }

        if (docRun.FontFamily is not null)
        {
            rp.Append(new W.RunFonts { Ascii = docRun.FontFamily, HighAnsi = docRun.FontFamily });
            hasProps = true;
        }

        if (docRun.FontSize is double size)
        {
            // OpenXML stores font size in half-points
            rp.Append(new W.FontSize { Val = ((int)(size * 2)).ToString(CultureInfo.InvariantCulture) });
            hasProps = true;
        }

        if (docRun.Color is not null)
        {
            rp.Append(new W.Color { Val = docRun.Color });
            hasProps = true;
        }

        if (hasProps) run.Append(rp);
        run.Append(new W.Text(docRun.Text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static void WriteInlineImage(MainDocumentPart mainPart, DocumentImage image)
    {
        var imagePart = mainPart.AddImagePart(
            image.ContentType switch
            {
                "image/png" => ImagePartType.Png,
                "image/gif" => ImagePartType.Gif,
                "image/bmp" => ImagePartType.Bmp,
                "image/tiff" => ImagePartType.Tiff,
                _ => ImagePartType.Jpeg,
            });

        using (var stream = new MemoryStream(image.Data))
        {
            imagePart.FeedData(stream);
        }

        string rId = mainPart.GetIdOfPart(imagePart);
        long cx = image.WidthEmu ?? 914400L * 4; // default 4 inches
        long cy = image.HeightEmu ?? 914400L * 3; // default 3 inches

        var drawing = new W.Drawing(
            new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = cx, Cy = cy },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties
                {
                    Id = 1,
                    Name = image.AltText ?? "Image",
                },
                new DocumentFormat.OpenXml.Drawing.Graphic(
                    new DocumentFormat.OpenXml.Drawing.GraphicData(
                        new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                            new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = 0, Name = "Image" },
                                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
                            new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                                new DocumentFormat.OpenXml.Drawing.Blip { Embed = rId },
                                new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                            new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                                new DocumentFormat.OpenXml.Drawing.Transform2D(
                                    new DocumentFormat.OpenXml.Drawing.Offset { X = 0, Y = 0 },
                                    new DocumentFormat.OpenXml.Drawing.Extents { Cx = cx, Cy = cy }),
                                new DocumentFormat.OpenXml.Drawing.PresetGeometry { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));

        mainPart.Document!.Body!.Append(new W.Paragraph(new W.Run(drawing)));
    }

    /// <summary>
    /// Creates a NumberingDefinitionsPart with bullet (numId=1) and numbered (numId=2) list definitions.
    /// </summary>
    private static void EnsureNumberingPart(MainDocumentPart mainPart)
    {
        var numberingPart = mainPart.AddNewPart<NumberingDefinitionsPart>();
        numberingPart.Numbering = new W.Numbering(
            // Abstract: bullet list
            new W.AbstractNum(
                new W.Level(
                    new W.NumberingFormat { Val = NumberFormatValues.Bullet },
                    new W.LevelText { Val = "\u2022" } // bullet character
                ) { LevelIndex = 0 }
            ) { AbstractNumberId = 1 },
            // Abstract: numbered list
            new W.AbstractNum(
                new W.Level(
                    new W.NumberingFormat { Val = NumberFormatValues.Decimal },
                    new W.LevelText { Val = "%1." }
                ) { LevelIndex = 0 }
            ) { AbstractNumberId = 2 },
            // Instance: bullet → numId 1
            new W.NumberingInstance(
                new W.AbstractNumId { Val = 1 }
            ) { NumberID = 1 },
            // Instance: numbered → numId 2
            new W.NumberingInstance(
                new W.AbstractNumId { Val = 2 }
            ) { NumberID = 2 }
        );
    }

    private static W.SectionProperties EnsureSectionProperties(MainDocumentPart mainPart)
    {
        var body = mainPart.Document!.Body!;
        var sectPr = body.Elements<W.SectionProperties>().FirstOrDefault();
        if (sectPr is null)
        {
            sectPr = new W.SectionProperties();
            body.Append(sectPr);
        }
        return sectPr;
    }

    private static PresentationShape ParsePptShape(DocumentFormat.OpenXml.Presentation.Shape shape)
    {
        var nvSpPr = shape.NonVisualShapeProperties;
        string name = nvSpPr?.NonVisualDrawingProperties?.Name?.Value ?? "Shape";

        // Position and size from ShapeProperties.Transform2D
        long? x = null, y = null, w = null, h = null;
        var xfrm = shape.ShapeProperties?.Transform2D;
        if (xfrm is not null)
        {
            x = xfrm.Offset?.X?.Value;
            y = xfrm.Offset?.Y?.Value;
            w = xfrm.Extents?.Cx?.Value;
            h = xfrm.Extents?.Cy?.Value;
        }

        // Text content
        var textRuns = new List<DocumentRun>();
        var textBody = shape.TextBody;
        if (textBody is not null)
        {
            foreach (var para in textBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>())
            {
                foreach (var run in para.Elements<DocumentFormat.OpenXml.Drawing.Run>())
                {
                    var rp = run.RunProperties;
                    textRuns.Add(new DocumentRun
                    {
                        Text = run.Text?.Text ?? "",
                        Bold = rp?.Bold?.Value ?? false,
                        Italic = rp?.Italic?.Value ?? false,
                        Underline = rp?.Underline is not null
                            && rp.Underline.Value != DocumentFormat.OpenXml.Drawing.TextUnderlineValues.None,
                        FontFamily = rp?.GetFirstChild<DocumentFormat.OpenXml.Drawing.LatinFont>()?.Typeface?.Value,
                        FontSize = rp?.FontSize?.Value is int sz ? sz / 100.0 : null,
                    });
                }
            }
        }

        var plainText = string.Join("", textRuns.Select(r => r.Text));

        return new PresentationShape
        {
            Name = name,
            Type = textBody is not null ? PresentationShapeType.TextBox : PresentationShapeType.Other,
            X = x, Y = y, Width = w, Height = h,
            TextRuns = textRuns,
            Text = string.IsNullOrWhiteSpace(plainText) ? null : plainText,
        };
    }

    private static SpreadsheetCell ParseExcelCell(
        S.Cell cell,
        string displayValue,
        S.SharedStringTable? sharedStrings,
        S.Stylesheet? stylesheet)
    {
        var address = cell.CellReference?.Value ?? "";
        var cellType = CellValueType.Empty;
        string? formula = null;

        if (cell.CellFormula is not null)
        {
            cellType = CellValueType.Formula;
            formula = cell.CellFormula.Text;
        }
        else if (cell.DataType?.Value == S.CellValues.SharedString)
        {
            cellType = CellValueType.Text;
        }
        else if (cell.DataType?.Value == S.CellValues.Boolean)
        {
            cellType = CellValueType.Boolean;
        }
        else if (cell.DataType?.Value == S.CellValues.InlineString)
        {
            cellType = CellValueType.Text;
        }
        else if (cell.CellValue is not null)
        {
            cellType = CellValueType.Number;
        }

        // Parse style
        CellStyle? style = null;
        if (stylesheet is not null && cell.StyleIndex?.Value is uint styleIdx)
        {
            style = ParseCellStyle(stylesheet, styleIdx);
        }

        return new SpreadsheetCell
        {
            Address = address,
            Value = string.IsNullOrEmpty(displayValue) ? null : displayValue,
            Type = cellType,
            Formula = formula,
            Style = style,
        };
    }

    private static CellStyle? ParseCellStyle(S.Stylesheet stylesheet, uint styleIndex)
    {
        var cellFormats = stylesheet.CellFormats;
        if (cellFormats is null || styleIndex >= cellFormats.Count?.Value)
            return null;

        var cf = cellFormats.Elements<S.CellFormat>().ElementAtOrDefault((int)styleIndex);
        if (cf is null) return null;

        bool bold = false;
        bool italic = false;
        string? fontColor = null;
        double? fontSize = null;
        string? fontFamily = null;
        string? bgColor = null;
        string? numberFormat = null;

        // Font
        if (cf.FontId?.Value is uint fontId && stylesheet.Fonts is not null)
        {
            var font = stylesheet.Fonts.Elements<S.Font>().ElementAtOrDefault((int)fontId);
            if (font is not null)
            {
                bold = font.Bold is not null;
                italic = font.Italic is not null;
                fontColor = font.Color?.Rgb?.Value;
                if (font.FontSize?.Val?.Value is double sz) fontSize = sz;
                fontFamily = font.FontName?.Val?.Value;
            }
        }

        // Fill
        if (cf.FillId?.Value is uint fillId && stylesheet.Fills is not null)
        {
            var fill = stylesheet.Fills.Elements<S.Fill>().ElementAtOrDefault((int)fillId);
            bgColor = fill?.PatternFill?.ForegroundColor?.Rgb?.Value;
        }

        // Number format
        if (cf.NumberFormatId?.Value is uint numFmtId && stylesheet.NumberingFormats is not null)
        {
            var nf = stylesheet.NumberingFormats.Elements<S.NumberingFormat>()
                .FirstOrDefault(n => n.NumberFormatId?.Value == numFmtId);
            numberFormat = nf?.FormatCode?.Value;
        }

        // Only return style if there's something meaningful
        if (!bold && !italic && fontColor is null && fontSize is null
            && fontFamily is null && bgColor is null && numberFormat is null)
            return null;

        return new CellStyle
        {
            Bold = bold,
            Italic = italic,
            FontColor = fontColor,
            BackgroundColor = bgColor,
            FontSize = fontSize,
            FontFamily = fontFamily,
            NumberFormat = numberFormat,
        };
    }

    private static int CountOccurrences(string source, string find)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(find, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += find.Length;
        }
        return count;
    }

    private static string GetCellValue(S.Cell cell, S.SharedStringTable? sharedStrings)
    {
        var value = cell.CellValue?.Text ?? string.Empty;

        if (cell.DataType?.Value == S.CellValues.SharedString &&
            sharedStrings is not null &&
            int.TryParse(value, out var index))
        {
            return sharedStrings.ElementAt(index).InnerText;
        }

        return value;
    }

    // ── Excel Write Helpers ──

    private static (S.Stylesheet stylesheet, Dictionary<string, uint> styleMap) BuildStylesheet(SpreadsheetContent content)
    {
        var fonts = new S.Fonts();
        var fills = new S.Fills();
        var borders = new S.Borders();
        var cellFormats = new S.CellFormats();
        var numberFormats = new S.NumberingFormats();

        // Default font (index 0)
        fonts.Append(new S.Font(new S.FontSize { Val = 11 }, new S.FontName { Val = "Calibri" }));

        // Default fills (index 0 = none, index 1 = gray125 — both required by Excel)
        fills.Append(new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }));
        fills.Append(new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 }));

        // Default border (index 0)
        borders.Append(new S.Border(new S.LeftBorder(), new S.RightBorder(), new S.TopBorder(), new S.BottomBorder(), new S.DiagonalBorder()));

        // Default cell format (index 0)
        cellFormats.Append(new S.CellFormat());

        uint nextNumberFormatId = 164; // Custom number formats start at 164

        // Collect unique styles from RichRows
        var styleMap = new Dictionary<string, uint>(); // styleKey → cellFormatIndex

        foreach (var sheet in content.Sheets)
        {
            foreach (var row in sheet.RichRows)
            {
                foreach (var cell in row.Cells)
                {
                    if (cell.Style is null) continue;
                    var key = GetStyleKey(cell.Style);
                    if (styleMap.ContainsKey(key)) continue;

                    // Create font
                    var font = new S.Font();
                    if (cell.Style.FontSize.HasValue)
                        font.Append(new S.FontSize { Val = cell.Style.FontSize.Value });
                    else
                        font.Append(new S.FontSize { Val = 11 });

                    if (cell.Style.FontFamily is not null)
                        font.Append(new S.FontName { Val = cell.Style.FontFamily });
                    else
                        font.Append(new S.FontName { Val = "Calibri" });

                    if (cell.Style.Bold)
                        font.Append(new S.Bold());
                    if (cell.Style.Italic)
                        font.Append(new S.Italic());
                    if (cell.Style.FontColor is not null)
                        font.Append(new S.Color { Rgb = cell.Style.FontColor });

                    var fontIndex = (uint)fonts.Count();
                    fonts.Append(font);

                    // Create fill
                    uint fillIndex = 0;
                    if (cell.Style.BackgroundColor is not null)
                    {
                        fillIndex = (uint)fills.Count();
                        fills.Append(new S.Fill(new S.PatternFill
                        {
                            PatternType = S.PatternValues.Solid,
                            ForegroundColor = new S.ForegroundColor { Rgb = cell.Style.BackgroundColor },
                        }));
                    }

                    // Create number format
                    uint? numFmtId = null;
                    if (cell.Style.NumberFormat is not null)
                    {
                        numFmtId = nextNumberFormatId++;
                        numberFormats.Append(new S.NumberingFormat
                        {
                            NumberFormatId = numFmtId.Value,
                            FormatCode = cell.Style.NumberFormat,
                        });
                    }

                    // Create cell format
                    var cf = new S.CellFormat
                    {
                        FontId = fontIndex,
                        FillId = fillIndex,
                        BorderId = 0,
                        ApplyFont = true,
                    };
                    if (fillIndex > 0) cf.ApplyFill = true;
                    if (numFmtId.HasValue)
                    {
                        cf.NumberFormatId = numFmtId.Value;
                        cf.ApplyNumberFormat = true;
                    }

                    var formatIndex = (uint)cellFormats.Count();
                    cellFormats.Append(cf);

                    styleMap[key] = formatIndex;
                }
            }
        }

        fonts.Count = (uint)fonts.Count();
        fills.Count = (uint)fills.Count();
        borders.Count = (uint)borders.Count();
        cellFormats.Count = (uint)cellFormats.Count();
        numberFormats.Count = (uint)numberFormats.Count();

        var stylesheet = new S.Stylesheet();
        if (numberFormats.Count > 0)
            stylesheet.Append(numberFormats);
        stylesheet.Append(fonts);
        stylesheet.Append(fills);
        stylesheet.Append(borders);
        stylesheet.Append(cellFormats);

        return (stylesheet, styleMap);
    }

    private static string GetStyleKey(CellStyle style)
    {
        return $"{style.Bold}|{style.Italic}|{style.FontColor}|{style.BackgroundColor}|{style.NumberFormat}|{style.FontSize}|{style.FontFamily}";
    }

    private static S.Cell BuildExcelCell(SpreadsheetCell cell, Dictionary<string, uint> styleMap)
    {
        var xlCell = new S.Cell { CellReference = cell.Address };

        // Set style index from pre-built style map
        if (cell.Style is not null)
        {
            var key = GetStyleKey(cell.Style);
            if (styleMap.TryGetValue(key, out var formatIndex))
            {
                xlCell.StyleIndex = formatIndex;
            }
        }

        // Handle formula
        if (cell.Formula is not null)
        {
            xlCell.CellFormula = new S.CellFormula(cell.Formula);
            if (cell.Value is not null)
                xlCell.CellValue = new S.CellValue(cell.Value);
            return xlCell;
        }

        // Handle typed values
        switch (cell.Type)
        {
            case CellValueType.Number:
                xlCell.DataType = S.CellValues.Number;
                xlCell.CellValue = new S.CellValue(cell.Value ?? "0");
                break;

            case CellValueType.Boolean:
                xlCell.DataType = S.CellValues.Boolean;
                xlCell.CellValue = new S.CellValue(
                    string.Equals(cell.Value, "TRUE", StringComparison.OrdinalIgnoreCase) ? "1" : "0");
                break;

            case CellValueType.Date:
                // Dates in Excel are stored as numbers (OLE Automation date)
                xlCell.CellValue = new S.CellValue(cell.Value ?? "");
                break;

            case CellValueType.Text:
            default:
                xlCell.DataType = S.CellValues.String;
                xlCell.CellValue = new S.CellValue(cell.Value ?? "");
                break;
        }

        return xlCell;
    }
}
