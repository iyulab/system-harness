namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class ObservationModelTests
{
    [Fact]
    public void Observation_RequiredTimestamp()
    {
        var ts = DateTimeOffset.UtcNow;
        using var obs = new Observation { Timestamp = ts };

        Assert.Equal(ts, obs.Timestamp);
        Assert.Null(obs.Screenshot);
        Assert.Null(obs.AccessibilityTree);
        Assert.Null(obs.OcrText);
        Assert.Null(obs.WindowInfo);
    }

    [Fact]
    public void Observation_FullyPopulated()
    {
        var ts = DateTimeOffset.UtcNow;
        var screenshot = new Screenshot
        {
            Bytes = [1, 2, 3],
            MimeType = "image/png",
            Width = 800,
            Height = 600,
            Timestamp = ts,
        };
        var tree = new UIElement { Name = "Root", ControlType = UIControlType.Window };
        var ocr = new OcrResult { Text = "Hello", Lines = [] };
        var window = new WindowInfo { Handle = 42, Title = "Test" };

        using var obs = new Observation
        {
            Timestamp = ts,
            Screenshot = screenshot,
            AccessibilityTree = tree,
            OcrText = ocr,
            WindowInfo = window,
        };

        Assert.Same(screenshot, obs.Screenshot);
        Assert.Same(tree, obs.AccessibilityTree);
        Assert.Same(ocr, obs.OcrText);
        Assert.Same(window, obs.WindowInfo);
    }

    [Fact]
    public void Observation_Dispose_DisposesScreenshot()
    {
        var screenshot = new Screenshot
        {
            Bytes = [1, 2, 3],
            MimeType = "image/png",
            Width = 10,
            Height = 10,
            Timestamp = DateTimeOffset.UtcNow,
        };

        var obs = new Observation { Timestamp = DateTimeOffset.UtcNow, Screenshot = screenshot };
        obs.Dispose();

        // Double dispose should not throw
        obs.Dispose();
    }

    [Fact]
    public void Observation_Dispose_WithoutScreenshot_NoThrow()
    {
        var obs = new Observation { Timestamp = DateTimeOffset.UtcNow };
        obs.Dispose(); // Should not throw even with null screenshot
    }

    [Fact]
    public void Observation_IsSealed()
    {
        Assert.True(typeof(Observation).IsSealed);
    }

    [Fact]
    public void Observation_ImplementsIDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(Observation)));
    }
}
