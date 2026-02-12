namespace SystemHarness.Apps.Office;

/// <summary>
/// Content of an Excel spreadsheet.
/// </summary>
public sealed class SpreadsheetContent
{
    /// <summary>
    /// Sheets in the workbook.
    /// </summary>
    public IReadOnlyList<SpreadsheetSheet> Sheets { get; init; } = [];
}

/// <summary>
/// A single sheet in a spreadsheet.
/// </summary>
public sealed class SpreadsheetSheet
{
    /// <summary>
    /// Sheet name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Rows of cell values. Each row is a list of cell strings.
    /// For basic usage — backward compatible with the original simple model.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];

    /// <summary>
    /// Rich cell data with types, formulas, and styles.
    /// When populated, provides more detail than <see cref="Rows"/>.
    /// </summary>
    public IReadOnlyList<SpreadsheetRow> RichRows { get; init; } = [];

    /// <summary>
    /// Merged cell ranges (e.g., "A1:C3").
    /// </summary>
    public IReadOnlyList<string> MergedCells { get; init; } = [];
}

/// <summary>
/// A row of cells in a spreadsheet.
/// </summary>
public sealed class SpreadsheetRow
{
    /// <summary>
    /// 1-based row index.
    /// </summary>
    public int RowIndex { get; init; }

    /// <summary>
    /// Cells in this row.
    /// </summary>
    public IReadOnlyList<SpreadsheetCell> Cells { get; init; } = [];
}

/// <summary>
/// A single cell with typed value, formula, and style information.
/// </summary>
public sealed class SpreadsheetCell
{
    /// <summary>
    /// Cell address (e.g., "A1", "B3").
    /// </summary>
    public required string Address { get; init; }

    /// <summary>
    /// Display value as string.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// The type of the cell value.
    /// </summary>
    public CellValueType Type { get; init; }

    /// <summary>
    /// Formula text without the leading '=' (e.g., "SUM(A1:A10)").
    /// Null if the cell does not contain a formula.
    /// </summary>
    public string? Formula { get; init; }

    /// <summary>
    /// Cell formatting/style information.
    /// </summary>
    public CellStyle? Style { get; init; }
}

/// <summary>
/// Type of a cell value.
/// </summary>
public enum CellValueType
{
    /// <summary>
    /// Empty cell.
    /// </summary>
    Empty,

    /// <summary>
    /// String/text value.
    /// </summary>
    String,

    /// <summary>
    /// Numeric value.
    /// </summary>
    Number,

    /// <summary>
    /// Boolean value (TRUE/FALSE).
    /// </summary>
    Boolean,

    /// <summary>
    /// Date/time value (stored as number in Excel).
    /// </summary>
    Date,

    /// <summary>
    /// Cell contains a formula (value is the cached result).
    /// </summary>
    Formula,
}

/// <summary>
/// Cell formatting/style information.
/// </summary>
public sealed class CellStyle
{
    /// <summary>
    /// Whether the text is bold.
    /// </summary>
    public bool Bold { get; init; }

    /// <summary>
    /// Whether the text is italic.
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// Font color as hex (e.g., "FF0000").
    /// </summary>
    public string? FontColor { get; init; }

    /// <summary>
    /// Background/fill color as hex.
    /// </summary>
    public string? BackgroundColor { get; init; }

    /// <summary>
    /// Number format string (e.g., "0.00", "yyyy-MM-dd", "#,##0").
    /// </summary>
    public string? NumberFormat { get; init; }

    /// <summary>
    /// Font size in points.
    /// </summary>
    public double? FontSize { get; init; }

    /// <summary>
    /// Font family name.
    /// </summary>
    public string? FontFamily { get; init; }
}
