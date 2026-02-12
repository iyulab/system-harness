namespace SystemHarness.Apps.Office;

/// <summary>
/// Content of a Word document.
/// </summary>
public sealed class DocumentContent
{
    /// <summary>
    /// Ordered list of paragraphs in the document.
    /// </summary>
    public IReadOnlyList<DocumentParagraph> Paragraphs { get; init; } = [];

    /// <summary>
    /// Tables in the document.
    /// </summary>
    public IReadOnlyList<DocumentTable> Tables { get; init; } = [];

    /// <summary>
    /// Images in the document.
    /// </summary>
    public IReadOnlyList<DocumentImage> Images { get; init; } = [];

    /// <summary>
    /// Header text (first section), if any.
    /// </summary>
    public string? HeaderText { get; init; }

    /// <summary>
    /// Footer text (first section), if any.
    /// </summary>
    public string? FooterText { get; init; }
}

/// <summary>
/// A single paragraph in a Word document.
/// </summary>
public sealed class DocumentParagraph
{
    /// <summary>
    /// Full plain text of the paragraph (convenience — concatenation of all run texts).
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Style name (e.g., "Heading1", "Normal").
    /// </summary>
    public string? Style { get; init; }

    /// <summary>
    /// Individual text runs with formatting information.
    /// Empty when formatting details are not needed.
    /// </summary>
    public IReadOnlyList<DocumentRun> Runs { get; init; } = [];

    /// <summary>
    /// List nesting level (0-based). Null if the paragraph is not a list item.
    /// </summary>
    public int? ListLevel { get; init; }

    /// <summary>
    /// Type of list this paragraph belongs to. Null if not a list item.
    /// </summary>
    public ListType? ListType { get; init; }
}

/// <summary>
/// A text run within a paragraph, carrying formatting information.
/// </summary>
public sealed class DocumentRun
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
    /// Whether the text has strikethrough.
    /// </summary>
    public bool Strikethrough { get; init; }

    /// <summary>
    /// Font family name (e.g., "Calibri", "Arial").
    /// </summary>
    public string? FontFamily { get; init; }

    /// <summary>
    /// Font size in points (e.g., 11.0, 14.0).
    /// </summary>
    public double? FontSize { get; init; }

    /// <summary>
    /// Text color as hex string (e.g., "FF0000" for red).
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Hyperlink URI if this run is part of a hyperlink.
    /// </summary>
    public string? HyperlinkUri { get; init; }
}

/// <summary>
/// An image embedded in a Word document.
/// </summary>
public sealed class DocumentImage
{
    /// <summary>
    /// Raw image bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// MIME content type (e.g., "image/png", "image/jpeg").
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Alternative text description.
    /// </summary>
    public string? AltText { get; init; }

    /// <summary>
    /// Image width in EMU (English Metric Units). 1 inch = 914400 EMU.
    /// </summary>
    public long? WidthEmu { get; init; }

    /// <summary>
    /// Image height in EMU (English Metric Units). 1 inch = 914400 EMU.
    /// </summary>
    public long? HeightEmu { get; init; }
}

/// <summary>
/// A table in a Word document.
/// </summary>
public sealed class DocumentTable
{
    /// <summary>
    /// Rows of the table, each containing a list of cell texts.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
}

/// <summary>
/// Type of list numbering.
/// </summary>
public enum ListType
{
    /// <summary>
    /// Bullet list (unordered).
    /// </summary>
    Bullet,

    /// <summary>
    /// Numbered list (ordered).
    /// </summary>
    Numbered,
}
