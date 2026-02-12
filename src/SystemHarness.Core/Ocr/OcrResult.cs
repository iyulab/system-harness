namespace SystemHarness;

/// <summary>
/// Result of an OCR recognition operation.
/// </summary>
public sealed class OcrResult
{
    /// <summary>
    /// The full recognized text (all lines concatenated).
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Individual recognized lines.
    /// </summary>
    public required IReadOnlyList<OcrLine> Lines { get; init; }

    /// <summary>
    /// The language used for recognition.
    /// </summary>
    public string? Language { get; init; }
}
