using SystemHarness.Windows;

namespace SystemHarness.Tests.Clipboard;

[Trait("Category", "Local")]
public class WindowsClipboardTests
{
    private readonly WindowsClipboard _clipboard = new();

    [Fact]
    public async Task SetTextAsync_AndGetTextAsync_RoundTrip()
    {
        var text = $"SystemHarness test {Guid.NewGuid():N}";

        await _clipboard.SetTextAsync(text, TestContext.Current.CancellationToken);
        var result = await _clipboard.GetTextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(text, result);
    }

    [Fact]
    public async Task SetTextAsync_EmptyString_Works()
    {
        await _clipboard.SetTextAsync("", TestContext.Current.CancellationToken);
        var result = await _clipboard.GetTextAsync(TestContext.Current.CancellationToken);

        Assert.Equal("", result);
    }

    [Fact]
    public async Task SetTextAsync_UnicodeText_PreservesCharacters()
    {
        var text = "Hello 안녕하세요 こんにちは 🎉";

        await _clipboard.SetTextAsync(text, TestContext.Current.CancellationToken);
        var result = await _clipboard.GetTextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(text, result);
    }

    [Fact]
    public async Task SetTextAsync_LongText_Works()
    {
        var text = new string('A', 10000);

        await _clipboard.SetTextAsync(text, TestContext.Current.CancellationToken);
        var result = await _clipboard.GetTextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(text, result);
    }

    [Fact]
    public async Task SetHtmlAsync_AndGetHtmlAsync_RoundTrip()
    {
        var html = "<p>Hello World</p>";

        await _clipboard.SetHtmlAsync(html, TestContext.Current.CancellationToken);
        var result = await _clipboard.GetHtmlAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Contains("Hello World", result);
    }

    [Fact]
    public async Task SetHtmlAsync_NonAsciiContent_PreservesUtf8()
    {
        // Regression test: CF_HTML byte offsets must account for multi-byte UTF-8
        var html = "<p>한글 テスト 🔥 emoji</p>";

        await _clipboard.SetHtmlAsync(html, TestContext.Current.CancellationToken);
        var result = await _clipboard.GetHtmlAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Contains("한글", result);
        Assert.Contains("テスト", result);
        Assert.Contains("🔥", result);
    }

    [Fact]
    public async Task SetImageAsync_AndGetImageAsync_RoundTrip()
    {
        // Create a minimal valid BMP (1x1 pixel, 24-bit)
        var bmp = CreateMinimalBmp();

        await _clipboard.SetImageAsync(bmp, TestContext.Current.CancellationToken);
        var result = await _clipboard.GetImageAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        // Result should be a valid BMP
        Assert.True(result.Length >= 14 + 40); // BMP header + BITMAPINFOHEADER
        Assert.Equal((byte)'B', result[0]);
        Assert.Equal((byte)'M', result[1]);
    }

    [Fact]
    public async Task SetImageAsync_InvalidData_ThrowsHarnessException()
    {
        await Assert.ThrowsAsync<HarnessException>(() =>
            _clipboard.SetImageAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetImageAsync_WhenClipboardHasText_ReturnsNull()
    {
        // Set text (not image) on clipboard
        await _clipboard.SetTextAsync("just text", TestContext.Current.CancellationToken);

        var result = await _clipboard.GetImageAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private static byte[] CreateMinimalBmp()
    {
        // BMP file header (14 bytes) + BITMAPINFOHEADER (40 bytes) + 1 pixel (4 bytes padded)
        var bmp = new byte[14 + 40 + 4];

        // BMP file header
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.TryWriteBytes(bmp.AsSpan(2), bmp.Length); // bfSize
        BitConverter.TryWriteBytes(bmp.AsSpan(10), 14 + 40);   // bfOffBits

        // BITMAPINFOHEADER
        BitConverter.TryWriteBytes(bmp.AsSpan(14), 40);        // biSize
        BitConverter.TryWriteBytes(bmp.AsSpan(18), 1);         // biWidth
        BitConverter.TryWriteBytes(bmp.AsSpan(22), 1);         // biHeight
        BitConverter.TryWriteBytes(bmp.AsSpan(26), (short)1);  // biPlanes
        BitConverter.TryWriteBytes(bmp.AsSpan(28), (short)24); // biBitCount
        // biCompression = 0 (BI_RGB), biSizeImage = 0 (fine for BI_RGB)

        // Pixel data: 1 BGR pixel (blue) + 1 byte padding to 4-byte boundary
        bmp[54] = 0xFF; // Blue
        bmp[55] = 0x00; // Green
        bmp[56] = 0x00; // Red

        return bmp;
    }
}
