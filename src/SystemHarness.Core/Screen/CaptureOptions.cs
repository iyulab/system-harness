namespace SystemHarness;

/// <summary>
/// Options for screen capture operations.
/// </summary>
public sealed class CaptureOptions
{
    /// <summary>
    /// Image format for the captured screenshot.
    /// Default is JPEG for smaller size and LLM compatibility.
    /// </summary>
    public ImageFormat Format { get; set; } = ImageFormat.Jpeg;

    /// <summary>
    /// JPEG quality (1-100). Only used when Format is JPEG.
    /// Default is 80, which balances quality and size for LLM consumption.
    /// </summary>
    public int Quality { get; set; } = 80;

    /// <summary>
    /// Target width for resizing. Null means no resize.
    /// Default is 1024 (optimal for LLM vision APIs).
    /// </summary>
    public int? TargetWidth { get; set; } = 1024;

    /// <summary>
    /// Target height for resizing. Null means no resize.
    /// Default is 768 (optimal for LLM vision APIs).
    /// </summary>
    public int? TargetHeight { get; set; } = 768;

    /// <summary>
    /// Whether to include the mouse cursor in the capture.
    /// Default is true (recommended for LLM agents to see cursor position).
    /// </summary>
    public bool IncludeCursor { get; set; } = true;
}

/// <summary>
/// Supported image formats for screenshot encoding.
/// </summary>
public enum ImageFormat
{
    Png,
    Jpeg
}
