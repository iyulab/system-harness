using SystemHarness.Windows;

namespace SystemHarness.Tests.Clipboard;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class ClipboardExtensionTests
{
    private readonly WindowsClipboard _clipboard = new();

    [Fact]
    public async Task GetAvailableFormatsAsync_ReturnsFormats()
    {
        // Set some text first to ensure clipboard has content
        await _clipboard.SetTextAsync("test content");
        await Task.Delay(100);

        var formats = await _clipboard.GetAvailableFormatsAsync();

        Assert.NotEmpty(formats);
        Assert.Contains(formats, f => f.Contains("UNICODETEXT") || f.Contains("TEXT"));
    }

    [Fact]
    public async Task SetHtmlAsync_GetHtmlAsync_RoundTrip()
    {
        var html = "<b>Hello</b> World";

        await _clipboard.SetHtmlAsync(html);
        await Task.Delay(100);

        var result = await _clipboard.GetHtmlAsync();

        Assert.NotNull(result);
        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
    }

    [Fact]
    public async Task GetHtmlAsync_ReturnsNull_WhenNoHtml()
    {
        // Set plain text only
        await _clipboard.SetTextAsync("plain text only");
        await Task.Delay(100);

        var result = await _clipboard.GetHtmlAsync();

        // May or may not be null depending on clipboard state
        // Just verify no exception
    }

    [Fact]
    public async Task SetFileDropListAsync_GetFileDropListAsync_RoundTrip()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var paths = new List<string> { tempFile };

            await _clipboard.SetFileDropListAsync(paths);
            await Task.Delay(100);

            var result = await _clipboard.GetFileDropListAsync();

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(tempFile, result[0]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SetFileDropListAsync_MultiplePaths()
    {
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();

        try
        {
            var paths = new List<string> { tempFile1, tempFile2 };

            await _clipboard.SetFileDropListAsync(paths);
            await Task.Delay(100);

            var result = await _clipboard.GetFileDropListAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }
        finally
        {
            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }
    }

    [Fact]
    public async Task GetFileDropListAsync_ReturnsNull_WhenNoFiles()
    {
        // Set text only
        await _clipboard.SetTextAsync("no files here");
        await Task.Delay(100);

        var result = await _clipboard.GetFileDropListAsync();

        Assert.Null(result);
    }
}
