namespace SystemHarness;

/// <summary>
/// OCR (Optical Character Recognition) — extract text from screenshots and images.
/// Useful for reading from apps that lack accessibility tree support.
/// </summary>
public interface IOcr
{
    /// <summary>
    /// Captures the full screen and recognizes text.
    /// </summary>
    Task<OcrResult> RecognizeScreenAsync(OcrOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Captures a screen region and recognizes text.
    /// </summary>
    Task<OcrResult> RecognizeRegionAsync(int x, int y, int width, int height, OcrOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Recognizes text from raw image data (PNG or JPEG bytes).
    /// </summary>
    Task<OcrResult> RecognizeImageAsync(byte[] imageData, OcrOptions? options = null, CancellationToken ct = default);
}
