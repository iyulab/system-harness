using SkiaSharp;
using SystemHarness.Windows;

namespace SystemHarness.Tests.Ocr;

/// <summary>
/// CI-safe OCR tests using programmatically generated images with known text.
/// Does not require desktop/screen — only uses RecognizeImageAsync.
/// </summary>
[Trait("Category", "CI")]
public class OcrImageRecognitionTests : IDisposable
{
    private readonly WindowsOcr _ocr;
    private readonly StubScreen _screen = new();

    public OcrImageRecognitionTests()
    {
        _ocr = new WindowsOcr(_screen);
    }

    public void Dispose() => _screen.Dispose();

    [Fact]
    public async Task RecognizeImage_SimpleEnglishText_ReturnsCorrectText()
    {
        var png = RenderTextToPng("Hello World", 48);
        var result = await _ocr.RecognizeImageAsync(png);

        Assert.Contains("Hello", result.Text);
        Assert.Contains("World", result.Text);
    }

    [Fact]
    public async Task RecognizeImage_MultipleLines_ReturnsMultipleLines()
    {
        var png = RenderTextToPng("First Line\nSecond Line\nThird Line", 36);
        var result = await _ocr.RecognizeImageAsync(png);

        Assert.True(result.Lines.Count >= 2, $"Expected >= 2 lines, got {result.Lines.Count}");
    }

    [Fact]
    public async Task RecognizeImage_Numbers_RecognizesDigits()
    {
        var png = RenderTextToPng("12345 67890", 48);
        var result = await _ocr.RecognizeImageAsync(png);

        Assert.Contains("12345", result.Text);
        Assert.Contains("67890", result.Text);
    }

    [Fact]
    public async Task RecognizeImage_WordBoundingRects_AreNonZero()
    {
        var png = RenderTextToPng("Test Bounding Rects", 48);
        var result = await _ocr.RecognizeImageAsync(png);

        var words = result.Lines.SelectMany(l => l.Words).ToList();
        Assert.True(words.Count >= 2, $"Expected >= 2 words, got {words.Count}");

        foreach (var word in words)
        {
            Assert.True(word.BoundingRect.Width > 0, $"Word '{word.Text}' should have positive width");
            Assert.True(word.BoundingRect.Height > 0, $"Word '{word.Text}' should have positive height");
            Assert.True(word.BoundingRect.X >= 0, $"Word '{word.Text}' X should be >= 0");
            Assert.True(word.BoundingRect.Y >= 0, $"Word '{word.Text}' Y should be >= 0");
        }
    }

    [Fact]
    public async Task RecognizeImage_LineBoundingRects_AreNonZero()
    {
        var png = RenderTextToPng("Line Bounds Test", 48);
        var result = await _ocr.RecognizeImageAsync(png);

        Assert.True(result.Lines.Count >= 1);
        foreach (var line in result.Lines)
        {
            Assert.True(line.BoundingRect.Width > 0, $"Line '{line.Text}' should have positive width");
            Assert.True(line.BoundingRect.Height > 0, $"Line '{line.Text}' should have positive height");
        }
    }

    [Fact]
    public async Task RecognizeImage_Language_DefaultsToEnUs()
    {
        var png = RenderTextToPng("Language Test", 48);
        var result = await _ocr.RecognizeImageAsync(png);

        Assert.Equal("en-US", result.Language);
    }

    [Fact]
    public async Task RecognizeImage_TextProperty_MatchesLinesConcatenation()
    {
        var png = RenderTextToPng("Consistency Check", 48);
        var result = await _ocr.RecognizeImageAsync(png);

        // result.Text should contain the text from all lines
        foreach (var line in result.Lines)
        {
            Assert.Contains(line.Text, result.Text);
        }
    }

    [Fact]
    public async Task RecognizeImage_WordTexts_FormLineText()
    {
        var png = RenderTextToPng("Words Form Lines", 48);
        var result = await _ocr.RecognizeImageAsync(png);

        foreach (var line in result.Lines)
        {
            // Each word's text should appear in the line text
            foreach (var word in line.Words)
            {
                Assert.Contains(word.Text, line.Text);
            }
        }
    }

    [Fact]
    public async Task RecognizeImage_UnsupportedLanguage_ThrowsHarnessException()
    {
        var png = RenderTextToPng("Error Test", 48);

        await Assert.ThrowsAsync<HarnessException>(() =>
            _ocr.RecognizeImageAsync(png, new OcrOptions { Language = "zz-ZZ" }));
    }

    [Fact]
    public async Task RecognizeImage_LargeText_RecognizesAccurately()
    {
        var png = RenderTextToPng("AUTOMATION", 72);
        var result = await _ocr.RecognizeImageAsync(png);

        Assert.Contains("AUTOMATION", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Renders text to a PNG byte array using SkiaSharp. White background, black text.
    /// </summary>
    private static byte[] RenderTextToPng(string text, float fontSize)
    {
        var lines = text.Split('\n');
        var lineHeight = fontSize * 1.5f;
        var width = 800;
        var height = (int)(lineHeight * lines.Length + fontSize);

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), fontSize);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };

        for (int i = 0; i < lines.Length; i++)
        {
            canvas.DrawText(lines[i], 20, fontSize + i * lineHeight, SKTextAlign.Left, font, paint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// Minimal IScreen stub — RecognizeImageAsync doesn't use the screen.
    /// </summary>
    private sealed class StubScreen : IScreen, IDisposable
    {
        public Task<Screenshot> CaptureAsync(CaptureOptions? options = null, CancellationToken ct = default)
            => throw new NotSupportedException("StubScreen does not support capture.");

        public Task<Screenshot> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken ct = default)
            => throw new NotSupportedException("StubScreen does not support capture.");

        public Task<Screenshot> CaptureWindowAsync(string titleOrHandle, CancellationToken ct = default)
            => throw new NotSupportedException("StubScreen does not support capture.");

        public void Dispose() { }
    }
}
