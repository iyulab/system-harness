namespace SystemHarness;

/// <summary>
/// Default implementation of <see cref="IObserver"/> using <see cref="IHarness"/> interfaces.
/// Combines screenshot, accessibility tree, and OCR into a single observation.
/// </summary>
public sealed class HarnessObserver : IObserver
{
    private readonly IHarness _harness;

    /// <summary>
    /// Creates a new observer backed by the given harness.
    /// </summary>
    public HarnessObserver(IHarness harness)
    {
        _harness = harness ?? throw new ArgumentNullException(nameof(harness));
    }

    /// <inheritdoc />
    public async Task<Observation> ObserveAsync(
        string titleOrHandle, ObserveOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ObserveOptions();

        // Capture screenshot (needed for OCR as well)
        Screenshot? screenshot = null;
        if (options.IncludeScreenshot || options.IncludeOcr)
        {
            screenshot = await _harness.Screen.CaptureWindowAsync(titleOrHandle, ct);
        }

        // Get accessibility tree
        UIElement? tree = null;
        if (options.IncludeAccessibilityTree)
        {
            tree = await _harness.UIAutomation.GetAccessibilityTreeAsync(
                titleOrHandle, options.AccessibilityTreeMaxDepth, ct);
        }

        // OCR from the screenshot
        OcrResult? ocr = null;
        if (options.IncludeOcr && screenshot is not null)
        {
            ocr = await _harness.Ocr.RecognizeImageAsync(screenshot.Bytes, options.OcrOptions, ct);
        }

        // Find window info
        WindowInfo? windowInfo = null;
        var windows = await _harness.Window.ListAsync(ct);
        if (nint.TryParse(titleOrHandle, out var handle))
        {
            windowInfo = windows.FirstOrDefault(w => w.Handle == handle);
        }
        windowInfo ??= windows.FirstOrDefault(w =>
            w.Title.Contains(titleOrHandle, StringComparison.OrdinalIgnoreCase));

        // Dispose screenshot if it was only used for OCR and caller didn't request it
        if (screenshot is not null && !options.IncludeScreenshot)
            screenshot.Dispose();

        return new Observation
        {
            Screenshot = options.IncludeScreenshot ? screenshot : null,
            AccessibilityTree = tree,
            OcrText = ocr,
            WindowInfo = windowInfo,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }
}
