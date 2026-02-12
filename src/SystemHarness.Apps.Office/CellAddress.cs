namespace SystemHarness.Apps.Office;

/// <summary>
/// Utility for Excel cell address conversions.
/// Converts between A1-style references and zero-based (row, col) indices.
/// </summary>
public static class CellAddress
{
    /// <summary>
    /// Convert a column index (0-based) to a column letter (A, B, ..., Z, AA, AB, ...).
    /// </summary>
    public static string ColumnToLetter(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);

        var result = "";
        var col = columnIndex;
        while (true)
        {
            result = (char)('A' + col % 26) + result;
            col = col / 26 - 1;
            if (col < 0) break;
        }
        return result;
    }

    /// <summary>
    /// Convert a column letter (A, B, ..., Z, AA, AB, ...) to a 0-based column index.
    /// </summary>
    public static int LetterToColumn(string columnLetter)
    {
        ArgumentException.ThrowIfNullOrEmpty(columnLetter);

        int result = 0;
        foreach (var ch in columnLetter.ToUpperInvariant())
        {
            if (ch < 'A' || ch > 'Z')
                throw new ArgumentException($"Invalid column letter: '{columnLetter}'", nameof(columnLetter));
            result = result * 26 + (ch - 'A' + 1);
        }
        return result - 1;
    }

    /// <summary>
    /// Create an A1-style cell reference from 0-based row and column indices.
    /// </summary>
    public static string FromIndices(int row, int column)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);

        return $"{ColumnToLetter(column)}{row + 1}";
    }

    /// <summary>
    /// Parse an A1-style cell reference to 0-based (row, column) indices.
    /// </summary>
    public static (int Row, int Column) Parse(string cellReference)
    {
        ArgumentException.ThrowIfNullOrEmpty(cellReference);

        int splitIndex = 0;
        for (int i = 0; i < cellReference.Length; i++)
        {
            if (char.IsDigit(cellReference[i]))
            {
                splitIndex = i;
                break;
            }
        }

        if (splitIndex == 0)
            throw new ArgumentException($"Invalid cell reference: '{cellReference}'", nameof(cellReference));

        var columnPart = cellReference[..splitIndex];
        var rowPart = cellReference[splitIndex..];

        if (!int.TryParse(rowPart, out var rowNumber) || rowNumber < 1)
            throw new ArgumentException($"Invalid cell reference: '{cellReference}'", nameof(cellReference));

        return (rowNumber - 1, LetterToColumn(columnPart));
    }

    /// <summary>
    /// Parse a range reference (e.g., "A1:C5") to start and end (row, column) indices (0-based).
    /// </summary>
    public static (int StartRow, int StartColumn, int EndRow, int EndColumn) ParseRange(string rangeReference)
    {
        ArgumentException.ThrowIfNullOrEmpty(rangeReference);

        var parts = rangeReference.Split(':');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid range reference: '{rangeReference}'", nameof(rangeReference));

        var start = Parse(parts[0]);
        var end = Parse(parts[1]);

        return (start.Row, start.Column, end.Row, end.Column);
    }

    /// <summary>
    /// Create a range reference from 0-based indices.
    /// </summary>
    public static string ToRange(int startRow, int startColumn, int endRow, int endColumn)
    {
        return $"{FromIndices(startRow, startColumn)}:{FromIndices(endRow, endColumn)}";
    }
}
