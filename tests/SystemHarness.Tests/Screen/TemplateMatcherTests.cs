using SkiaSharp;
using SystemHarness.Windows;

namespace SystemHarness.Tests.Screen;

[Trait("Category", "CI")]
public class TemplateMatcherTests : IDisposable
{
    private readonly SkiaTemplateMatcher _matcher = new();
    private readonly List<string> _tempFiles = [];

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var f in _tempFiles)
            File.Delete(f);
    }

    [Fact]
    public async Task FindAsync_ExactMatch_ReturnsHighConfidence()
    {
        // Create a 100x100 noise image, embed a 20x20 checkerboard at (30, 40)
        var pattern = CreateCheckerboard(20, 20, 4);
        var source = CreateNoiseImage(100, 100, seed: 42);
        EmbedPattern(source, 100, pattern, 20, 20, 30, 40);
        var template = ExtractRegion(source, 100, 20, 20, 30, 40);

        var results = await _matcher.FindAsync(
            ToScreenshot(source, 100, 100), SaveTemp(template, 20, 20), 0.8);

        Assert.NotEmpty(results);
        var best = results[0];
        Assert.InRange(best.Confidence, 0.9, 1.0);
        Assert.InRange(best.X, 28, 32);
        Assert.InRange(best.Y, 38, 42);
        Assert.Equal(20, best.Width);
        Assert.Equal(20, best.Height);
    }

    [Fact]
    public async Task FindAsync_NoMatch_ReturnsEmpty()
    {
        // Checkerboard template not present in random noise
        var source = CreateNoiseImage(100, 100, seed: 42);
        var template = CreateCheckerboard(20, 20, 4);

        var results = await _matcher.FindAsync(
            ToScreenshot(source, 100, 100), SaveTemp(template, 20, 20), 0.9);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindAsync_MultipleMatches_ReturnsAll()
    {
        // Embed the same pattern at two distant locations
        var pattern = CreateCheckerboard(20, 20, 4);
        var source = CreateNoiseImage(200, 100, seed: 42);
        EmbedPattern(source, 200, pattern, 20, 20, 10, 10);
        EmbedPattern(source, 200, pattern, 20, 20, 160, 10);
        var template = ExtractRegion(source, 200, 20, 20, 10, 10);

        var results = await _matcher.FindAsync(
            ToScreenshot(source, 200, 100), SaveTemp(template, 20, 20), 0.8);

        Assert.True(results.Count >= 2, $"Expected >=2 matches, got {results.Count}");
    }

    [Fact]
    public async Task FindAsync_TemplateLargerThanSource_ReturnsEmpty()
    {
        var source = CreateNoiseImage(10, 10, seed: 1);
        var template = CreateCheckerboard(20, 20, 4);

        var results = await _matcher.FindAsync(
            ToScreenshot(source, 10, 10), SaveTemp(template, 20, 20), 0.5);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindAsync_InvalidTemplatePath_ThrowsHarnessException()
    {
        var source = CreateNoiseImage(50, 50, seed: 1);

        await Assert.ThrowsAsync<HarnessException>(
            () => _matcher.FindAsync(ToScreenshot(source, 50, 50), "nonexistent.png", 0.8));
    }

    [Fact]
    public async Task FindAsync_UniformImage_ReturnsEmpty()
    {
        // Uniform source and template — zero variance, no match possible
        var source = CreateSolidImage(100, 100, 128);
        var template = CreateSolidImage(20, 20, 128);

        var results = await _matcher.FindAsync(
            ToScreenshot(source, 100, 100), SaveTemp(template, 20, 20), 0.8);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindAsync_CancellationRespected()
    {
        var source = CreateNoiseImage(100, 100, seed: 42);
        var template = CreateCheckerboard(20, 20, 4);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _matcher.FindAsync(
                ToScreenshot(source, 100, 100), SaveTemp(template, 20, 20), 0.8, cts.Token));
    }

    [Fact]
    public async Task FindAsync_CenterCoordinates_Correct()
    {
        var pattern = CreateCheckerboard(20, 20, 4);
        var source = CreateNoiseImage(100, 100, seed: 42);
        EmbedPattern(source, 100, pattern, 20, 20, 40, 40);
        var template = ExtractRegion(source, 100, 20, 20, 40, 40);

        var results = await _matcher.FindAsync(
            ToScreenshot(source, 100, 100), SaveTemp(template, 20, 20), 0.8);

        Assert.NotEmpty(results);
        var best = results[0];
        Assert.Equal(best.X + best.Width / 2, best.CenterX);
        Assert.Equal(best.Y + best.Height / 2, best.CenterY);
    }

    [Fact]
    public async Task FindAsync_HighThreshold_FiltersWeakMatches()
    {
        var pattern = CreateCheckerboard(20, 20, 4);
        var source = CreateNoiseImage(100, 100, seed: 42);
        EmbedPattern(source, 100, pattern, 20, 20, 30, 40);
        var templatePath = SaveTemp(
            ExtractRegion(source, 100, 20, 20, 30, 40), 20, 20);
        var screenshot = ToScreenshot(source, 100, 100);

        var highResults = await _matcher.FindAsync(screenshot, templatePath, 0.99);
        var lowResults = await _matcher.FindAsync(screenshot, templatePath, 0.5);

        Assert.True(lowResults.Count >= highResults.Count);
    }

    // --- Image Creation Helpers ---

    private static byte[] CreateCheckerboard(int w, int h, int cellSize)
    {
        var gray = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                gray[y * w + x] = (byte)(((x / cellSize) + (y / cellSize)) % 2 == 0 ? 200 : 50);
        return gray;
    }

    private static byte[] CreateNoiseImage(int w, int h, int seed)
    {
        var rng = new Random(seed);
        var gray = new byte[w * h];
        rng.NextBytes(gray);
        return gray;
    }

    private static byte[] CreateSolidImage(int w, int h, byte value)
    {
        var gray = new byte[w * h];
        Array.Fill(gray, value);
        return gray;
    }

    private static void EmbedPattern(byte[] target, int targetW,
        byte[] pattern, int patW, int patH, int ox, int oy)
    {
        for (var y = 0; y < patH; y++)
            for (var x = 0; x < patW; x++)
                target[(oy + y) * targetW + (ox + x)] = pattern[y * patW + x];
    }

    private static byte[] ExtractRegion(byte[] source, int sourceW,
        int w, int h, int ox, int oy)
    {
        var region = new byte[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                region[y * w + x] = source[(oy + y) * sourceW + (ox + x)];
        return region;
    }

    /// <summary>Convert grayscale byte array to PNG-encoded Screenshot.</summary>
    private static Screenshot ToScreenshot(byte[] gray, int w, int h)
    {
        var pngBytes = GrayToPng(gray, w, h);
        return new Screenshot
        {
            Bytes = pngBytes,
            MimeType = "image/png",
            Width = w,
            Height = h,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>Save grayscale byte array as PNG temp file.</summary>
    private string SaveTemp(byte[] gray, int w, int h)
    {
        var path = Path.Combine(Path.GetTempPath(), $"harness-test-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, GrayToPng(gray, w, h));
        _tempFiles.Add(path);
        return path;
    }

    private static byte[] GrayToPng(byte[] gray, int w, int h)
    {
        using var bitmap = new SKBitmap(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var v = gray[y * w + x];
                bitmap.SetPixel(x, y, new SKColor(v, v, v));
            }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
