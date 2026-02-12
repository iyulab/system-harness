namespace SystemHarness.Apps.Office;

/// <summary>
/// Content of a HWP/HWPX document.
/// </summary>
public sealed class HwpContent
{
    /// <summary>
    /// Ordered sections of the document.
    /// Most documents have a single section.
    /// </summary>
    public IReadOnlyList<HwpSection> Sections { get; init; } = [];
}

/// <summary>
/// A section of a HWP document (corresponds to a sectionN.xml in HWPX).
/// </summary>
public sealed class HwpSection
{
    /// <summary>
    /// Paragraphs in this section.
    /// </summary>
    public IReadOnlyList<HwpParagraph> Paragraphs { get; init; } = [];

    /// <summary>
    /// Tables in this section.
    /// </summary>
    public IReadOnlyList<HwpTable> Tables { get; init; } = [];

    /// <summary>
    /// Images in this section.
    /// </summary>
    public IReadOnlyList<HwpImage> Images { get; init; } = [];
}

/// <summary>
/// A paragraph in a HWP document.
/// </summary>
public sealed class HwpParagraph
{
    /// <summary>
    /// Full plain text of the paragraph.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Text runs with formatting information.
    /// </summary>
    public IReadOnlyList<HwpRun> Runs { get; init; } = [];

    /// <summary>
    /// Index referencing the paragraph shape (style) in the header.
    /// </summary>
    public int? ParaShapeId { get; init; }
}

/// <summary>
/// A text run in a HWP paragraph with formatting.
/// </summary>
public sealed class HwpRun
{
    /// <summary>
    /// Text content of this run.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Whether the text is bold.
    /// </summary>
    public bool Bold { get; init; }

    /// <summary>
    /// Whether the text is italic.
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// Whether the text is underlined.
    /// </summary>
    public bool Underline { get; init; }

    /// <summary>
    /// Font family name (e.g., "맑은 고딕", "바탕").
    /// </summary>
    public string? FontFamily { get; init; }

    /// <summary>
    /// Font size in points.
    /// </summary>
    public double? FontSize { get; init; }

    /// <summary>
    /// Text color as hex string (e.g., "000000").
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Index referencing the character shape in the header.
    /// </summary>
    public int? CharShapeId { get; init; }
}

/// <summary>
/// A table in a HWP document.
/// </summary>
public sealed class HwpTable
{
    /// <summary>
    /// Number of rows.
    /// </summary>
    public int RowCount { get; init; }

    /// <summary>
    /// Number of columns.
    /// </summary>
    public int ColCount { get; init; }

    /// <summary>
    /// Rows of the table, each containing cell texts.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
}

/// <summary>
/// An image in a HWP document.
/// </summary>
public sealed class HwpImage
{
    /// <summary>
    /// Raw image bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// MIME content type (e.g., "image/png").
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// BinData item ID reference within the HWPX package.
    /// </summary>
    public string? BinDataId { get; init; }

    /// <summary>
    /// Width in HWPML units.
    /// </summary>
    public long? Width { get; init; }

    /// <summary>
    /// Height in HWPML units.
    /// </summary>
    public long? Height { get; init; }
}
