namespace SystemHarness;

/// <summary>
/// A hybrid observation combining screenshot, accessibility tree, and OCR results.
/// Provides a complete view of the current state of a window for AI agents.
/// Implements <see cref="IDisposable"/> to ensure the <see cref="Screenshot"/> is properly released.
/// </summary>
public sealed class Observation : IDisposable
{
    /// <summary>
    /// Screenshot of the window (if requested).
    /// </summary>
    public Screenshot? Screenshot { get; init; }

    /// <summary>
    /// Accessibility tree of the window (if requested).
    /// </summary>
    public UIElement? AccessibilityTree { get; init; }

    /// <summary>
    /// OCR text recognition result (if requested).
    /// </summary>
    public OcrResult? OcrText { get; init; }

    /// <summary>
    /// Information about the observed window.
    /// </summary>
    public WindowInfo? WindowInfo { get; init; }

    /// <summary>
    /// Timestamp when the observation was taken.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Disposes the contained screenshot (if any) to free image buffer memory.
    /// </summary>
    public void Dispose()
    {
        Screenshot?.Dispose();
    }
}
