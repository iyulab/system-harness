namespace SystemHarness;

/// <summary>
/// A recognized line of text from OCR.
/// </summary>
public sealed class OcrLine
{
    /// <summary>
    /// The full text of this line.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Individual words in this line.
    /// </summary>
    public required IReadOnlyList<OcrWord> Words { get; init; }

    /// <summary>
    /// Bounding rectangle of the line in screen coordinates.
    /// </summary>
    public Rectangle BoundingRect { get; init; }
}
