namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class ScreenModelTests
{
    [Fact]
    public void Screenshot_RequiredProperties()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var timestamp = DateTimeOffset.UtcNow;

        using var screenshot = new Screenshot
        {
            Bytes = bytes,
            MimeType = "image/png",
            Width = 1920,
            Height = 1080,
            Timestamp = timestamp,
        };

        Assert.Equal(bytes, screenshot.Bytes);
        Assert.Equal("image/png", screenshot.MimeType);
        Assert.Equal(1920, screenshot.Width);
        Assert.Equal(1080, screenshot.Height);
        Assert.Equal(timestamp, screenshot.Timestamp);
    }

    [Fact]
    public void Screenshot_Base64_ComputedProperty()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        using var screenshot = new Screenshot
        {
            Bytes = bytes,
            MimeType = "image/png",
            Width = 10,
            Height = 10,
            Timestamp = DateTimeOffset.UtcNow,
        };

        Assert.Equal(Convert.ToBase64String(bytes), screenshot.Base64);
        Assert.Equal("AQIDBAU=", screenshot.Base64);
    }

    [Fact]
    public void Screenshot_EmptyBytes_Base64Empty()
    {
        using var screenshot = new Screenshot
        {
            Bytes = [],
            MimeType = "image/jpeg",
            Width = 0,
            Height = 0,
            Timestamp = DateTimeOffset.UtcNow,
        };

        Assert.Equal("", screenshot.Base64);
    }

    [Fact]
    public async Task Screenshot_SaveAsync_WritesFile()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var screenshot = new Screenshot
        {
            Bytes = bytes,
            MimeType = "image/png",
            Width = 1,
            Height = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var path = Path.Combine(Path.GetTempPath(), $"screenshot-test-{Guid.NewGuid()}.png");
        try
        {
            await screenshot.SaveAsync(path);
            Assert.True(File.Exists(path));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Screenshot_Dispose_CanBeCalledMultipleTimes()
    {
        var screenshot = new Screenshot
        {
            Bytes = [1, 2, 3],
            MimeType = "image/png",
            Width = 1,
            Height = 1,
            Timestamp = DateTimeOffset.UtcNow,
        };

        screenshot.Dispose();
        screenshot.Dispose(); // should not throw
    }

    [Fact]
    public void Screenshot_JpegMimeType()
    {
        using var screenshot = new Screenshot
        {
            Bytes = [0xFF, 0xD8, 0xFF],
            MimeType = "image/jpeg",
            Width = 640,
            Height = 480,
            Timestamp = DateTimeOffset.UtcNow,
        };

        Assert.Equal("image/jpeg", screenshot.MimeType);
    }
}
