using SystemHarness.Windows;

namespace SystemHarness.Tests.Ocr;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
[Trait("Category", "RequiresDesktop")]
public class WindowsOcrTests : IDisposable
{
    private readonly WindowsScreen _screen = new();
    private readonly WindowsOcr _ocr;

    public WindowsOcrTests(DesktopInteractionFixture fixture)
    {
        _ocr = new WindowsOcr(_screen);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _screen.Dispose();
    }

    [Fact]
    public async Task RecognizeScreen_ReturnsNonEmptyResult()
    {
        var result = await _ocr.RecognizeScreenAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.Text);
        // Desktop usually has some text visible (taskbar, window titles, icons)
        Assert.True(result.Text.Length > 0, "OCR should detect at least some text on the desktop");
        Assert.Equal("en-US", result.Language);
    }

    [Fact]
    public async Task RecognizeImage_WithScreenshot_Works()
    {
        using var screenshot = await _screen.CaptureAsync();
        var result = await _ocr.RecognizeImageAsync(screenshot.Bytes);

        Assert.NotNull(result);
        Assert.NotNull(result.Lines);
        Assert.True(result.Lines.Count > 0, "OCR should detect lines from a screenshot");
    }

    [Fact]
    public async Task RecognizeScreen_HasLines()
    {
        var result = await _ocr.RecognizeScreenAsync();

        Assert.NotNull(result.Lines);
        Assert.True(result.Lines.Count > 0, "Desktop should have at least some OCR-detectable lines");
    }

    [Fact]
    public async Task RecognizeScreen_WordsHaveBoundingRects()
    {
        var result = await _ocr.RecognizeScreenAsync();

        Assert.NotNull(result.Lines);
        var allWords = result.Lines.SelectMany(l => l.Words).ToList();
        Assert.True(allWords.Count > 0, "Should have at least one word");

        // All words should have non-zero bounding rectangles
        foreach (var word in allWords.Take(10))
        {
            Assert.True(word.BoundingRect.Width > 0, $"Word '{word.Text}' should have positive width");
            Assert.True(word.BoundingRect.Height > 0, $"Word '{word.Text}' should have positive height");
        }
    }

    [Fact]
    public async Task RecognizeImage_UnsupportedLanguage_ThrowsHarnessException()
    {
        using var screenshot = await _screen.CaptureAsync();

        await Assert.ThrowsAsync<HarnessException>(() =>
            _ocr.RecognizeImageAsync(screenshot.Bytes, new OcrOptions { Language = "zz-ZZ" }));
    }
}
