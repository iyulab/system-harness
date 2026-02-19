using System.Globalization;
using SystemHarness.Windows;

namespace SystemHarness.Tests.Screen;

[Trait("Category", "Local")]
public class WindowsScreenTests : IDisposable
{
    private readonly WindowsScreen _screen = new();
    private readonly string _tempDir;

    public WindowsScreenTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SystemHarness.Tests.Screen", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task CaptureAsync_DefaultOptions_ReturnsJpeg()
    {
        using var screenshot = await _screen.CaptureAsync();

        Assert.NotNull(screenshot);
        Assert.True(screenshot.Bytes.Length > 0);
        Assert.Equal("image/jpeg", screenshot.MimeType);
        Assert.True(screenshot.Width > 0);
        Assert.True(screenshot.Height > 0);
        Assert.True(screenshot.Timestamp > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task CaptureAsync_JpegFormat_ProducesJpeg()
    {
        var options = new CaptureOptions { Format = ImageFormat.Jpeg, Quality = 80 };
        using var screenshot = await _screen.CaptureAsync(options);

        Assert.Equal("image/jpeg", screenshot.MimeType);
        // JPEG magic bytes: FF D8
        Assert.Equal(0xFF, screenshot.Bytes[0]);
        Assert.Equal(0xD8, screenshot.Bytes[1]);
    }

    [Fact]
    public async Task CaptureAsync_PngFormat_ProducesPng()
    {
        var options = new CaptureOptions
        {
            Format = ImageFormat.Png,
            TargetWidth = null,
            TargetHeight = null,
        };
        using var screenshot = await _screen.CaptureAsync(options);

        Assert.Equal("image/png", screenshot.MimeType);
        // PNG magic bytes: 89 50 4E 47
        Assert.Equal(0x89, screenshot.Bytes[0]);
        Assert.Equal(0x50, screenshot.Bytes[1]);
        Assert.Equal(0x4E, screenshot.Bytes[2]);
        Assert.Equal(0x47, screenshot.Bytes[3]);
    }

    [Fact]
    public async Task CaptureAsync_WithResize_ResizesToTarget()
    {
        var options = new CaptureOptions
        {
            Format = ImageFormat.Png,
            TargetWidth = 640,
            TargetHeight = 480,
        };
        using var screenshot = await _screen.CaptureAsync(options);

        Assert.Equal(640, screenshot.Width);
        Assert.Equal(480, screenshot.Height);
    }

    [Fact]
    public async Task CaptureAsync_NoResize_KeepsOriginalSize()
    {
        var options = new CaptureOptions
        {
            TargetWidth = null,
            TargetHeight = null,
        };
        using var screenshot = await _screen.CaptureAsync(options);

        // Should be screen resolution (at least 800x600)
        Assert.True(screenshot.Width >= 800);
        Assert.True(screenshot.Height >= 600);
    }

    [Fact]
    public async Task CaptureAsync_Base64_IsValid()
    {
        using var screenshot = await _screen.CaptureAsync();

        var base64 = screenshot.Base64;
        Assert.NotNull(base64);
        Assert.NotEmpty(base64);

        // Should be valid base64
        var decoded = Convert.FromBase64String(base64);
        Assert.Equal(screenshot.Bytes.Length, decoded.Length);
    }

    [Fact]
    public async Task CaptureRegionAsync_CapturesSubset()
    {
        using var screenshot = await _screen.CaptureRegionAsync(0, 0, 200, 200);

        Assert.NotNull(screenshot);
        Assert.True(screenshot.Bytes.Length > 0);
    }

    [Fact]
    public async Task CaptureWindowAsync_CapturesExistingWindow()
    {
        // Use an already-open window instead of launching notepad
        var windowApi = new WindowsWindow();
        var windows = await windowApi.ListAsync();
        var target = windows.FirstOrDefault(w => w.Bounds.Width > 100 && w.Bounds.Height > 100);

        Assert.NotNull(target);

        using var screenshot = await _screen.CaptureWindowAsync(target.Handle.ToString(CultureInfo.InvariantCulture));

        Assert.NotNull(screenshot);
        Assert.True(screenshot.Bytes.Length > 0);
        Assert.True(screenshot.Width > 0);
        Assert.True(screenshot.Height > 0);
    }

    [Fact]
    public async Task SaveAsync_WritesToFile()
    {
        using var screenshot = await _screen.CaptureAsync(new CaptureOptions
        {
            TargetWidth = 320,
            TargetHeight = 240,
        });

        var filePath = Path.Combine(_tempDir, "capture.jpg");
        await screenshot.SaveAsync(filePath);

        Assert.True(File.Exists(filePath));
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        Assert.Equal(screenshot.Bytes.Length, fileBytes.Length);
    }

    [Fact]
    public async Task CaptureWindowAsync_NonExistent_ThrowsHarnessException()
    {
        await Assert.ThrowsAsync<HarnessException>(() =>
            _screen.CaptureWindowAsync("NonExistentWindow_XYZ_99999"));
    }
}
