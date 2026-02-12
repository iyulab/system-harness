namespace SystemHarness;

/// <summary>
/// Options for hybrid observation of a window.
/// </summary>
public sealed class ObserveOptions
{
    /// <summary>
    /// Include a screenshot of the window. Default is true.
    /// </summary>
    public bool IncludeScreenshot { get; set; } = true;

    /// <summary>
    /// Include the accessibility tree. Default is true.
    /// </summary>
    public bool IncludeAccessibilityTree { get; set; } = true;

    /// <summary>
    /// Include OCR text recognition. Default is false (slower).
    /// </summary>
    public bool IncludeOcr { get; set; }

    /// <summary>
    /// Maximum depth for the accessibility tree traversal. Default is 5.
    /// </summary>
    public int AccessibilityTreeMaxDepth { get; set; } = 5;

    /// <summary>
    /// OCR options (language, region). Only used when <see cref="IncludeOcr"/> is true.
    /// </summary>
    public OcrOptions? OcrOptions { get; set; }
}
