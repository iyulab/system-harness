using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace SystemHarness.Apps.Office;

/// <summary>
/// HWPX (OWPML) format reader and writer.
/// Parses .hwpx files which are ZIP archives containing XML per the KS X 6101 standard.
/// </summary>
public sealed class HwpxReader : IHwpReader
{
    // OWPML namespace URIs
    private static readonly XNamespace NsParagraph = "http://www.hancom.co.kr/hwpml/2011/paragraph";
    private static readonly XNamespace NsHead = "http://www.hancom.co.kr/hwpml/2011/head";
    private static readonly XNamespace NsSection = "http://www.hancom.co.kr/hwpml/2011/section";
    private static readonly XNamespace NsCore = "http://www.hancom.co.kr/hwpml/2011/core";

    // Element names — section/paragraph
    private static readonly XName ElParagraph = NsParagraph + "p";
    private static readonly XName ElRun = NsParagraph + "run";
    private static readonly XName ElText = NsParagraph + "t";
    private static readonly XName ElTable = NsParagraph + "tbl";
    private static readonly XName ElTableRow = NsParagraph + "tr";
    private static readonly XName ElTableCell = NsParagraph + "tc";
    private static readonly XName ElPicture = NsParagraph + "pic";

    // Element names — header charPr
    private static readonly XName ElCharPr = NsHead + "charPr";
    private static readonly XName ElCharProperties = NsHead + "charProperties";
    private static readonly XName ElRefList = NsHead + "refList";
    private static readonly XName ElBold = NsHead + "bold";
    private static readonly XName ElItalic = NsHead + "italic";
    private static readonly XName ElUnderline = NsHead + "underline";
    private static readonly XName ElFontRef = NsHead + "fontRef";

    public Task<HwpContent> ReadHwpxAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var zip = ZipFile.OpenRead(filePath);

            // Validate mimetype
            var mimetypeEntry = zip.GetEntry("mimetype");
            if (mimetypeEntry is not null)
            {
                using var reader = new StreamReader(mimetypeEntry.Open());
                var mime = reader.ReadToEnd().Trim();
                // HWPX mimetype should be "application/hwp+zip" or similar
            }

            // Parse header.xml for character shape definitions
            var charShapeMap = ParseHeaderCharShapes(zip);

            // Find section files
            var sections = new List<HwpSection>();
            var binDataMap = BuildBinDataMap(zip);

            for (int i = 0; ; i++)
            {
                ct.ThrowIfCancellationRequested();

                var sectionEntry = zip.GetEntry($"Contents/section{i}.xml");
                if (sectionEntry is null) break;

                using var stream = sectionEntry.Open();
                var xdoc = XDocument.Load(stream);
                sections.Add(ParseSection(xdoc, binDataMap, charShapeMap));
            }

            // If no numbered sections found, try "Contents/section.xml"
            if (sections.Count == 0)
            {
                var sectionEntry = zip.GetEntry("Contents/section.xml");
                if (sectionEntry is not null)
                {
                    using var stream = sectionEntry.Open();
                    var xdoc = XDocument.Load(stream);
                    sections.Add(ParseSection(xdoc, binDataMap, charShapeMap));
                }
            }

            return new HwpContent { Sections = sections };
        }, ct);
    }

    public async Task<int> FindReplaceAsync(string filePath, string find, string replace, CancellationToken ct = default)
    {
        var content = await ReadHwpxAsync(filePath, ct);
        int count = 0;

        var newSections = new List<HwpSection>();
        foreach (var section in content.Sections)
        {
            var newParagraphs = new List<HwpParagraph>();
            foreach (var para in section.Paragraphs)
            {
                var (newPara, paraCount) = ReplaceParagraphText(para, find, replace);
                newParagraphs.Add(newPara);
                count += paraCount;
            }

            var newTables = new List<HwpTable>();
            foreach (var table in section.Tables)
            {
                var newRows = new List<IReadOnlyList<string>>();
                foreach (var row in table.Rows)
                {
                    var newCells = new List<string>();
                    foreach (var cell in row)
                    {
                        int cellCount = CountOccurrences(cell, find);
                        count += cellCount;
                        newCells.Add(cell.Replace(find, replace, StringComparison.Ordinal));
                    }
                    newRows.Add(newCells);
                }
                newTables.Add(new HwpTable
                {
                    RowCount = table.RowCount,
                    ColCount = table.ColCount,
                    Rows = newRows,
                });
            }

            newSections.Add(new HwpSection
            {
                Paragraphs = newParagraphs,
                Tables = newTables,
                Images = section.Images,
            });
        }

        if (count > 0)
        {
            var newContent = new HwpContent { Sections = newSections };
            await WriteHwpxAsync(filePath, newContent, ct);
        }

        return count;
    }

    private static (HwpParagraph para, int count) ReplaceParagraphText(HwpParagraph para, string find, string replace)
    {
        int count = 0;

        if (para.Runs.Count > 0)
        {
            var newRuns = new List<HwpRun>();
            foreach (var run in para.Runs)
            {
                int runCount = CountOccurrences(run.Text, find);
                count += runCount;
                newRuns.Add(new HwpRun
                {
                    Text = run.Text.Replace(find, replace, StringComparison.Ordinal),
                    CharShapeId = run.CharShapeId,
                    Bold = run.Bold,
                    Italic = run.Italic,
                    Underline = run.Underline,
                    FontFamily = run.FontFamily,
                    FontSize = run.FontSize,
                    Color = run.Color,
                });
            }

            var newText = string.Join("", newRuns.Select(r => r.Text));
            return (new HwpParagraph
            {
                Text = newText,
                Runs = newRuns,
                ParaShapeId = para.ParaShapeId,
            }, count);
        }

        count = CountOccurrences(para.Text, find);
        return (new HwpParagraph
        {
            Text = para.Text.Replace(find, replace, StringComparison.Ordinal),
            Runs = para.Runs,
            ParaShapeId = para.ParaShapeId,
        }, count);
    }

    private static int CountOccurrences(string text, string find)
    {
        if (string.IsNullOrEmpty(find)) return 0;
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(find, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += find.Length;
        }
        return count;
    }

    public Task WriteHwpxAsync(string filePath, HwpContent content, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            // Delete existing file if any
            if (File.Exists(filePath))
                File.Delete(filePath);

            using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);

            // Write mimetype (must be first entry, uncompressed)
            var mimetypeEntry = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimetypeEntry.Open()))
            {
                writer.Write("application/hwp+zip");
            }

            // Write META-INF/container.xml
            WriteContainerXml(zip);

            // Write Contents/content.hpf
            WriteContentHpf(zip, content.Sections.Count);

            // Write Contents/header.xml (with charProperties from runs)
            WriteHeaderXml(zip, content);

            // Write section files and collect image data for BinData/
            int imageIndex = 0;
            var pendingImages = new List<(string name, byte[] data)>();

            for (int i = 0; i < content.Sections.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var sectionEntry = zip.CreateEntry($"Contents/section{i}.xml");
                using (var stream = sectionEntry.Open())
                {
                    WriteSectionXml(stream, content.Sections[i], ref imageIndex, pendingImages);
                }
            }

            // Write BinData entries after all section streams are closed
            foreach (var (name, data) in pendingImages)
            {
                var binEntry = zip.CreateEntry($"BinData/{name}");
                using var binStream = binEntry.Open();
                binStream.Write(data, 0, data.Length);
            }
        }, ct);
    }

    // ── Read Helpers ──

    private static Dictionary<string, byte[]> BuildBinDataMap(ZipArchive zip)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.StartsWith("BinData/", StringComparison.OrdinalIgnoreCase)
                && entry.Length > 0)
            {
                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                map[entry.Name] = ms.ToArray();
            }
        }
        return map;
    }

    /// <summary>
    /// Parse charProperties from header.xml → map of id → (bold, italic, underline, fontFamily, fontSize, color).
    /// </summary>
    private static Dictionary<int, CharShapeInfo> ParseHeaderCharShapes(ZipArchive zip)
    {
        var map = new Dictionary<int, CharShapeInfo>();

        var headerEntry = zip.GetEntry("Contents/header.xml");
        if (headerEntry is null) return map;

        using var stream = headerEntry.Open();
        var xdoc = XDocument.Load(stream);
        var root = xdoc.Root;
        if (root is null) return map;

        // Navigate: <hh:head> → <hh:refList> → <hh:charProperties> → <hh:charPr id="N" ...>
        var charPropertiesElem = root.Descendants(ElCharProperties).FirstOrDefault();
        if (charPropertiesElem is null) return map;

        foreach (var charPrElem in charPropertiesElem.Elements(ElCharPr))
        {
            if (!int.TryParse(charPrElem.Attribute("id")?.Value, out var id))
                continue;

            var info = new CharShapeInfo
            {
                Bold = charPrElem.Element(ElBold) is not null,
                Italic = charPrElem.Element(ElItalic) is not null,
                Underline = charPrElem.Element(ElUnderline) is not null,
            };

            // height attribute = font size in hundredths of a point (e.g. 1000 = 10pt)
            if (int.TryParse(charPrElem.Attribute("height")?.Value, out var height))
                info.FontSize = height / 100.0;

            // textColor attribute = hex color (e.g. "000000")
            info.Color = charPrElem.Attribute("textColor")?.Value;

            // fontRef child element — HANGUL or LATIN font face
            var fontRefElem = charPrElem.Element(ElFontRef);
            if (fontRefElem is not null)
            {
                info.FontFamily = fontRefElem.Attribute("hangul")?.Value
                    ?? fontRefElem.Attribute("latin")?.Value;
            }

            map[id] = info;
        }

        return map;
    }

    private sealed class CharShapeInfo
    {
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Underline { get; set; }
        public string? FontFamily { get; set; }
        public double? FontSize { get; set; }
        public string? Color { get; set; }
    }

    private static HwpSection ParseSection(XDocument xdoc, Dictionary<string, byte[]> binDataMap, Dictionary<int, CharShapeInfo> charShapeMap)
    {
        var root = xdoc.Root;
        if (root is null) return new HwpSection();

        var paragraphs = new List<HwpParagraph>();
        var tables = new List<HwpTable>();
        var images = new List<HwpImage>();

        // Parse all paragraphs at any depth
        foreach (var pElem in root.Descendants(ElParagraph))
        {
            // Skip paragraphs that are inside table cells (they'll be handled by table parsing)
            if (pElem.Ancestors(ElTableCell).Any()) continue;

            paragraphs.Add(ParseParagraph(pElem, charShapeMap));
        }

        // Parse tables
        foreach (var tblElem in root.Descendants(ElTable))
        {
            tables.Add(ParseTable(tblElem));
        }

        // Parse images
        foreach (var picElem in root.Descendants(ElPicture))
        {
            var img = ParseImage(picElem, binDataMap);
            if (img is not null) images.Add(img);
        }

        return new HwpSection
        {
            Paragraphs = paragraphs,
            Tables = tables,
            Images = images,
        };
    }

    private static HwpParagraph ParseParagraph(XElement pElem, Dictionary<int, CharShapeInfo> charShapeMap)
    {
        var runs = new List<HwpRun>();

        foreach (var runElem in pElem.Elements(ElRun))
        {
            var textElem = runElem.Element(ElText);
            var text = textElem?.Value ?? "";
            if (string.IsNullOrEmpty(text)) continue;

            // CharShapeId is typically in the run's charPrIDRef attribute
            int? charShapeId = null;
            if (int.TryParse(runElem.Attribute("charPrIDRef")?.Value, out var csId))
                charShapeId = csId;

            var run = new HwpRun
            {
                Text = text,
                CharShapeId = charShapeId,
            };

            // Resolve formatting from header charShapeMap
            if (charShapeId.HasValue && charShapeMap.TryGetValue(charShapeId.Value, out var info))
            {
                run = new HwpRun
                {
                    Text = text,
                    CharShapeId = charShapeId,
                    Bold = info.Bold,
                    Italic = info.Italic,
                    Underline = info.Underline,
                    FontFamily = info.FontFamily,
                    FontSize = info.FontSize,
                    Color = info.Color,
                };
            }

            runs.Add(run);
        }

        var plainText = string.Join("", runs.Select(r => r.Text));

        // ParaShapeId
        int? paraShapeId = null;
        if (int.TryParse(pElem.Attribute("paraPrIDRef")?.Value, out var psId))
            paraShapeId = psId;

        return new HwpParagraph
        {
            Text = plainText,
            Runs = runs,
            ParaShapeId = paraShapeId,
        };
    }

    private static HwpTable ParseTable(XElement tblElem)
    {
        var rows = new List<IReadOnlyList<string>>();

        foreach (var trElem in tblElem.Elements(ElTableRow))
        {
            var cells = new List<string>();
            foreach (var tcElem in trElem.Elements(ElTableCell))
            {
                // Collect text from all paragraphs within the cell
                var cellText = string.Join("\n",
                    tcElem.Descendants(ElText).Select(t => t.Value));
                cells.Add(cellText);
            }
            rows.Add(cells);
        }

        int rowCount = rows.Count;
        int colCount = rows.Count > 0 ? rows.Max(r => r.Count) : 0;

        return new HwpTable
        {
            RowCount = rowCount,
            ColCount = colCount,
            Rows = rows,
        };
    }

    private static HwpImage? ParseImage(XElement picElem, Dictionary<string, byte[]> binDataMap)
    {
        // The picture element references a BinData item
        var binItemRef = picElem.Attribute("itemIDRef")?.Value
            ?? picElem.Descendants().FirstOrDefault(e => e.Name.LocalName == "binItem")
                ?.Attribute("IDRef")?.Value;

        if (binItemRef is null) return null;

        // Try to find the binary data
        foreach (var kvp in binDataMap)
        {
            if (kvp.Key.Contains(binItemRef, StringComparison.OrdinalIgnoreCase)
                || Path.GetFileNameWithoutExtension(kvp.Key)
                    .Equals(binItemRef, StringComparison.OrdinalIgnoreCase))
            {
                var ext = Path.GetExtension(kvp.Key).ToLowerInvariant();
                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    _ => "application/octet-stream",
                };

                return new HwpImage
                {
                    Data = kvp.Value,
                    ContentType = contentType,
                    BinDataId = binItemRef,
                };
            }
        }

        return null;
    }

    // ── Write Helpers ──

    private static void WriteContainerXml(ZipArchive zip)
    {
        var entry = zip.CreateEntry("META-INF/container.xml");
        using var stream = entry.Open();
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("container",
                new XAttribute("version", "1.0"),
                new XElement("rootfiles",
                    new XElement("rootfile",
                        new XAttribute("full-path", "Contents/content.hpf"),
                        new XAttribute("media-type", "application/hwpml-package+xml")))));
        doc.Save(stream);
    }

    private static void WriteContentHpf(ZipArchive zip, int sectionCount)
    {
        var entry = zip.CreateEntry("Contents/content.hpf");
        using var stream = entry.Open();

        var opfNs = XNamespace.Get("http://www.idpf.org/2007/opf");
        var dcNs = XNamespace.Get("http://purl.org/dc/elements/1.1/");

        var manifest = new XElement(opfNs + "manifest",
            new XElement(opfNs + "item",
                new XAttribute("id", "header"),
                new XAttribute("href", "header.xml"),
                new XAttribute("media-type", "application/xml")));

        var spine = new XElement(opfNs + "spine");

        for (int i = 0; i < sectionCount; i++)
        {
            var id = $"section{i}";
            manifest.Add(new XElement(opfNs + "item",
                new XAttribute("id", id),
                new XAttribute("href", $"{id}.xml"),
                new XAttribute("media-type", "application/xml")));
            spine.Add(new XElement(opfNs + "itemref",
                new XAttribute("idref", id)));
        }

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(opfNs + "package",
                new XAttribute("version", "1.2"),
                new XElement(opfNs + "metadata",
                    new XElement(dcNs + "title", "Document"),
                    new XElement(dcNs + "language", "ko")),
                manifest,
                spine));
        doc.Save(stream);
    }

    private static void WriteHeaderXml(ZipArchive zip, HwpContent content)
    {
        var entry = zip.CreateEntry("Contents/header.xml");
        using var stream = entry.Open();

        var headElem = new XElement(NsHead + "head",
            new XAttribute(XNamespace.Xmlns + "hh", NsHead.NamespaceName),
            new XAttribute("secCnt", content.Sections.Count.ToString(CultureInfo.InvariantCulture)));

        // Collect unique charShapeIds from all runs and write charProperties
        var charShapes = new Dictionary<int, HwpRun>();
        foreach (var section in content.Sections)
        {
            foreach (var para in section.Paragraphs)
            {
                foreach (var run in para.Runs)
                {
                    if (run.CharShapeId.HasValue && !charShapes.ContainsKey(run.CharShapeId.Value))
                        charShapes[run.CharShapeId.Value] = run;
                }
            }
        }

        if (charShapes.Count > 0)
        {
            var charPropertiesElem = new XElement(ElCharProperties,
                new XAttribute("itemCnt", charShapes.Count));

            foreach (var (id, run) in charShapes.OrderBy(kv => kv.Key))
            {
                var charPrElem = new XElement(ElCharPr,
                    new XAttribute("id", id));

                if (run.FontSize.HasValue)
                    charPrElem.SetAttributeValue("height", (int)(run.FontSize.Value * 100));

                if (run.Color is not null)
                    charPrElem.SetAttributeValue("textColor", run.Color);

                if (run.Bold) charPrElem.Add(new XElement(ElBold));
                if (run.Italic) charPrElem.Add(new XElement(ElItalic));
                if (run.Underline) charPrElem.Add(new XElement(ElUnderline));

                if (run.FontFamily is not null)
                {
                    charPrElem.Add(new XElement(ElFontRef,
                        new XAttribute("hangul", run.FontFamily),
                        new XAttribute("latin", run.FontFamily)));
                }

                charPropertiesElem.Add(charPrElem);
            }

            var refListElem = new XElement(ElRefList, charPropertiesElem);
            headElem.Add(refListElem);
        }

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), headElem);
        doc.Save(stream);
    }

    private static void WriteSectionXml(
        Stream stream,
        HwpSection section,
        ref int imageIndex,
        List<(string name, byte[] data)> pendingImages)
    {
        var root = new XElement(NsSection + "sec",
            new XAttribute(XNamespace.Xmlns + "hp", NsParagraph.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "hs", NsSection.NamespaceName));

        // Write paragraphs
        foreach (var para in section.Paragraphs)
        {
            var pElem = new XElement(ElParagraph);
            if (para.ParaShapeId is not null)
                pElem.SetAttributeValue("paraPrIDRef", para.ParaShapeId);

            if (para.Runs.Count > 0)
            {
                foreach (var run in para.Runs)
                {
                    var runElem = new XElement(ElRun,
                        new XElement(ElText, run.Text));
                    if (run.CharShapeId is not null)
                        runElem.SetAttributeValue("charPrIDRef", run.CharShapeId);
                    pElem.Add(runElem);
                }
            }
            else if (!string.IsNullOrEmpty(para.Text))
            {
                pElem.Add(new XElement(ElRun,
                    new XElement(ElText, para.Text)));
            }

            root.Add(pElem);
        }

        // Write tables
        foreach (var table in section.Tables)
        {
            var tblElem = new XElement(ElTable);
            foreach (var row in table.Rows)
            {
                var trElem = new XElement(ElTableRow);
                foreach (var cell in row)
                {
                    var tcElem = new XElement(ElTableCell,
                        new XElement(ElParagraph,
                            new XElement(ElRun,
                                new XElement(ElText, cell))));
                    trElem.Add(tcElem);
                }
                tblElem.Add(trElem);
            }
            root.Add(tblElem);
        }

        // Write image references (collect data for deferred BinData/ writing)
        foreach (var image in section.Images)
        {
            var binName = $"image{imageIndex++}{GetImageExtension(image.ContentType)}";
            pendingImages.Add((binName, image.Data));

            root.Add(new XElement(ElPicture,
                new XAttribute("itemIDRef", Path.GetFileNameWithoutExtension(binName))));
        }

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        doc.Save(stream);
    }

    private static string GetImageExtension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/bmp" => ".bmp",
        _ => ".jpg",
    };
}
