namespace SystemHarness;

/// <summary>
/// A single recognized word from OCR.
/// </summary>
public sealed class OcrWord
{
    /// <summary>
    /// The recognized text.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Bounding rectangle of the word in screen coordinates.
    /// </summary>
    public Rectangle BoundingRect { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) if available, null otherwise.
    /// </summary>
    public double? Confidence { get; init; }
}
