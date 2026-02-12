using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IOcr"/> using Windows.Media.Ocr (WinRT API).
/// Available on Windows 10+ without additional dependencies.
/// </summary>
public sealed class WindowsOcr : IOcr
{
    private readonly IScreen _screen;

    /// <summary>
    /// Capture options that preserve original resolution for accurate OCR bounding rectangles.
    /// </summary>
    private static readonly CaptureOptions OcrCaptureOptions = new()
    {
        TargetWidth = null,
        TargetHeight = null,
        Format = ImageFormat.Png,
        IncludeCursor = false,
    };

    public WindowsOcr(IScreen screen)
    {
        _screen = screen;
    }

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeScreenAsync(OcrOptions? options = null, CancellationToken ct = default)
    {
        using var screenshot = await _screen.CaptureAsync(OcrCaptureOptions, ct);
        return await RecognizeImageAsync(screenshot.Bytes, options, ct);
    }

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeRegionAsync(
        int x, int y, int width, int height, OcrOptions? options = null, CancellationToken ct = default)
    {
        using var screenshot = await _screen.CaptureRegionAsync(x, y, width, height, OcrCaptureOptions, ct);
        return await RecognizeImageAsync(screenshot.Bytes, options, ct);
    }

    /// <inheritdoc />
    public async Task<OcrResult> RecognizeImageAsync(
        byte[] imageData, OcrOptions? options = null, CancellationToken ct = default)
    {
        options ??= new OcrOptions();

        var language = new global::Windows.Globalization.Language(options.Language);
        if (!OcrEngine.IsLanguageSupported(language))
            throw new HarnessException($"OCR language not supported: {options.Language}");

        var engine = OcrEngine.TryCreateFromLanguage(language)
            ?? throw new HarnessException($"Could not create OCR engine for language: {options.Language}");

        using var bitmap = await LoadSoftwareBitmapAsync(imageData);

        var result = await engine.RecognizeAsync(bitmap);

        var lines = result.Lines.Select(line =>
        {
            var words = line.Words.Select(word => new OcrWord
            {
                Text = word.Text,
                BoundingRect = new Rectangle(
                    (int)word.BoundingRect.X,
                    (int)word.BoundingRect.Y,
                    (int)word.BoundingRect.Width,
                    (int)word.BoundingRect.Height),
            }).ToList();

            // Compute line bounding rect from words
            var lineRect = ComputeBoundingRect(words);

            return new OcrLine
            {
                Text = line.Text,
                Words = words,
                BoundingRect = lineRect,
            };
        }).ToList();

        return new OcrResult
        {
            Text = result.Text,
            Lines = lines,
            Language = options.Language,
        };
    }

    private static async Task<SoftwareBitmap> LoadSoftwareBitmapAsync(byte[] imageData)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(imageData);
            await writer.StoreAsync();
            await writer.FlushAsync();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    private static Rectangle ComputeBoundingRect(List<OcrWord> words)
    {
        if (words.Count == 0)
            return default;

        var minX = words.Min(w => w.BoundingRect.X);
        var minY = words.Min(w => w.BoundingRect.Y);
        var maxX = words.Max(w => w.BoundingRect.X + w.BoundingRect.Width);
        var maxY = words.Max(w => w.BoundingRect.Y + w.BoundingRect.Height);

        return new Rectangle(minX, minY, maxX - minX, maxY - minY);
    }
}
