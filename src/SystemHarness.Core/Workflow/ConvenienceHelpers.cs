namespace SystemHarness;

/// <summary>
/// Composite convenience operations that combine Screen, OCR, and Mouse primitives.
/// Follows the same pattern as <see cref="WaitHelpers"/>: static methods taking <see cref="IHarness"/> as the first parameter.
/// </summary>
public static class ConvenienceHelpers
{
    /// <summary>
    /// Capture options that preserve original resolution (no resize) for OCR coordinate accuracy.
    /// </summary>
    private static readonly CaptureOptions OcrCaptureOptions = new()
    {
        TargetWidth = null,
        TargetHeight = null,
        Format = ImageFormat.Png,
        IncludeCursor = false,
    };

    /// <summary>
    /// Captures the full screen at original resolution and performs OCR.
    /// </summary>
    /// <returns>A tuple of the screenshot and OCR result with accurate screen coordinates.</returns>
    public static async Task<(Screenshot Screenshot, OcrResult Ocr)> CaptureAndRecognizeAsync(
        IHarness harness, CancellationToken ct = default)
    {
        var screenshot = await harness.Screen.CaptureAsync(OcrCaptureOptions, ct);
        var ocr = await harness.Ocr.RecognizeImageAsync(screenshot.Bytes, ct: ct);
        return (screenshot, ocr);
    }

    /// <summary>
    /// Captures a specific window at original resolution and performs OCR.
    /// </summary>
    public static async Task<(Screenshot Screenshot, OcrResult Ocr)> CaptureAndRecognizeWindowAsync(
        IHarness harness, string titleOrHandle, CancellationToken ct = default)
    {
        var screenshot = await harness.Screen.CaptureWindowAsync(titleOrHandle, OcrCaptureOptions, ct);
        var ocr = await harness.Ocr.RecognizeImageAsync(screenshot.Bytes, ct: ct);
        return (screenshot, ocr);
    }

    /// <summary>
    /// Captures a screen region at original resolution and performs OCR.
    /// </summary>
    public static async Task<(Screenshot Screenshot, OcrResult Ocr)> CaptureAndRecognizeRegionAsync(
        IHarness harness, int x, int y, int width, int height, CancellationToken ct = default)
    {
        var screenshot = await harness.Screen.CaptureRegionAsync(x, y, width, height, OcrCaptureOptions, ct);
        var ocr = await harness.Ocr.RecognizeImageAsync(screenshot.Bytes, ct: ct);
        return (screenshot, ocr);
    }

    /// <summary>
    /// Captures the full screen, performs OCR, and returns all words matching the specified text.
    /// </summary>
    /// <param name="harness">The harness instance.</param>
    /// <param name="text">Text to search for (case-insensitive substring match on each word).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching <see cref="OcrWord"/> instances with accurate screen coordinates.</returns>
    public static async Task<IReadOnlyList<OcrWord>> FindTextOnScreenAsync(
        IHarness harness, string text, CancellationToken ct = default)
    {
        var (screenshot, ocr) = await CaptureAndRecognizeAsync(harness, ct);
        screenshot.Dispose();
        return FindMatchingWords(ocr, text);
    }

    /// <summary>
    /// Captures a window, performs OCR, and returns all words matching the specified text.
    /// </summary>
    public static async Task<IReadOnlyList<OcrWord>> FindTextInWindowAsync(
        IHarness harness, string titleOrHandle, string text, CancellationToken ct = default)
    {
        var (screenshot, ocr) = await CaptureAndRecognizeWindowAsync(harness, titleOrHandle, ct);
        screenshot.Dispose();
        return FindMatchingWords(ocr, text);
    }

    /// <summary>
    /// Captures the full screen, performs OCR, finds the first matching word, and clicks its center.
    /// </summary>
    /// <exception cref="HarnessException">Thrown when the specified text is not found on screen.</exception>
    public static async Task ClickTextOnScreenAsync(
        IHarness harness, string text, CancellationToken ct = default)
    {
        var words = await FindTextOnScreenAsync(harness, text, ct);
        if (words.Count == 0)
            throw new HarnessException($"Text not found on screen: {text}");

        var (x, y) = CoordinateHelpers.Center(words[0]);
        await harness.Mouse.ClickAsync(x, y, ct: ct);
    }

    /// <summary>
    /// Captures a window, performs OCR, finds the first matching word, and clicks its center.
    /// </summary>
    /// <exception cref="HarnessException">Thrown when the specified text is not found in the window.</exception>
    public static async Task ClickTextInWindowAsync(
        IHarness harness, string titleOrHandle, string text, CancellationToken ct = default)
    {
        var words = await FindTextInWindowAsync(harness, titleOrHandle, text, ct);
        if (words.Count == 0)
            throw new HarnessException($"Text not found in window {titleOrHandle}: {text}");

        var (x, y) = CoordinateHelpers.Center(words[0]);
        await harness.Mouse.ClickAsync(x, y, ct: ct);
    }

    private static List<OcrWord> FindMatchingWords(OcrResult ocr, string text)
    {
        var results = new List<OcrWord>();
        foreach (var line in ocr.Lines)
        {
            foreach (var word in line.Words)
            {
                if (word.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
                    results.Add(word);
            }
        }
        return results;
    }
}
