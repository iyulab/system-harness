namespace SystemHarness.Tests;

[Trait("Category", "CI")]
public class SmokeTests
{
    [Fact]
    public void CoreAssembly_CanBeLoaded()
    {
        var type = typeof(IHarness);
        Assert.NotNull(type);
        Assert.True(type.IsInterface);
    }

    [Fact]
    public void IHarness_ExposeAllServices()
    {
        var properties = typeof(IHarness).GetProperties();
        var names = properties.Select(p => p.Name).ToHashSet();

        // Layer 1: Programmatic
        Assert.Contains("Shell", names);
        Assert.Contains("Process", names);
        Assert.Contains("FileSystem", names);
        Assert.Contains("Window", names);
        Assert.Contains("Clipboard", names);
        Assert.Contains("SystemInfo", names);

        // Layer 2: Vision+Action
        Assert.Contains("Screen", names);
        Assert.Contains("Mouse", names);
        Assert.Contains("Keyboard", names);

        // Extended services
        Assert.Contains("Display", names);
        Assert.Contains("VirtualDesktop", names);
        Assert.Contains("DialogHandler", names);
        Assert.Contains("UIAutomation", names);
        Assert.Contains("Ocr", names);
        Assert.Contains("TemplateMatcher", names);
    }

    [Fact]
    public void IHarness_PropertyCount_MatchesExpected()
    {
        var properties = typeof(IHarness).GetProperties();
        // 15 services total — if a new service is added, this test should be updated
        Assert.Equal(15, properties.Length);
    }

    [Fact]
    public void ShellResult_SuccessProperty()
    {
        var success = new ShellResult { ExitCode = 0, StdOut = "ok", StdErr = "", Elapsed = TimeSpan.FromMilliseconds(100) };
        var failure = new ShellResult { ExitCode = 1, StdOut = "", StdErr = "error", Elapsed = TimeSpan.FromMilliseconds(50) };

        Assert.True(success.Success);
        Assert.False(failure.Success);
    }

    [Fact]
    public void Screenshot_Base64Encoding()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic bytes
        var screenshot = new Screenshot
        {
            Bytes = bytes,
            MimeType = "image/png",
            Width = 100,
            Height = 100,
            Timestamp = DateTimeOffset.UtcNow
        };

        Assert.Equal(Convert.ToBase64String(bytes), screenshot.Base64);
    }

    [Fact]
    public void CaptureOptions_Defaults()
    {
        var options = new CaptureOptions();

        Assert.Equal(ImageFormat.Jpeg, options.Format);
        Assert.Equal(80, options.Quality);
        Assert.Equal(1024, options.TargetWidth);
        Assert.Equal(768, options.TargetHeight);
        Assert.True(options.IncludeCursor);
    }

    // --- Additional smoke tests (cycle 228) ---

    [Fact]
    public void WindowInfo_RequiredProperties()
    {
        var win = new WindowInfo
        {
            Handle = 12345,
            Title = "Test Window",
            ProcessId = 42,
            Bounds = new Rectangle(10, 20, 800, 600)
        };

        Assert.Equal(12345, win.Handle);
        Assert.Equal("Test Window", win.Title);
        Assert.Equal(42, win.ProcessId);
        Assert.Equal(new Rectangle(10, 20, 800, 600), win.Bounds);
    }

    [Fact]
    public void WindowState_HasExpectedValues()
    {
        // Verify all expected states exist
        Assert.True(Enum.IsDefined(WindowState.Normal));
        Assert.True(Enum.IsDefined(WindowState.Minimized));
        Assert.True(Enum.IsDefined(WindowState.Maximized));
    }

    [Fact]
    public void MouseButton_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(MouseButton.Left));
        Assert.True(Enum.IsDefined(MouseButton.Right));
        Assert.True(Enum.IsDefined(MouseButton.Middle));
    }

    [Fact]
    public void ImageFormat_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(ImageFormat.Png));
        Assert.True(Enum.IsDefined(ImageFormat.Jpeg));
    }

    [Fact]
    public void ShellResult_ElapsedTracked()
    {
        var result = new ShellResult
        {
            ExitCode = 0,
            StdOut = "",
            StdErr = "",
            Elapsed = TimeSpan.FromSeconds(2.5)
        };

        Assert.Equal(2500, result.Elapsed.TotalMilliseconds);
    }

    [Fact]
    public void CaptureOptions_CustomValues()
    {
        var options = new CaptureOptions
        {
            Format = ImageFormat.Png,
            Quality = 100,
            TargetWidth = 1920,
            TargetHeight = 1080,
            IncludeCursor = false
        };

        Assert.Equal(ImageFormat.Png, options.Format);
        Assert.Equal(100, options.Quality);
        Assert.Equal(1920, options.TargetWidth);
        Assert.Equal(1080, options.TargetHeight);
        Assert.False(options.IncludeCursor);
    }
}
