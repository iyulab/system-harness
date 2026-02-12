namespace SystemHarness.Tests.Workflow;

[Trait("Category", "CI")]
public class CoordinateHelpersTests
{
    [Fact]
    public void WindowToScreen_AddsOffset()
    {
        var window = new WindowInfo
        {
            Handle = 1,
            Title = "Test",
            Bounds = new Rectangle(100, 200, 800, 600),
        };

        var (x, y) = CoordinateHelpers.WindowToScreen(window, 10, 20);

        Assert.Equal(110, x);
        Assert.Equal(220, y);
    }

    [Fact]
    public void ScreenToWindow_SubtractsOffset()
    {
        var window = new WindowInfo
        {
            Handle = 1,
            Title = "Test",
            Bounds = new Rectangle(100, 200, 800, 600),
        };

        var (x, y) = CoordinateHelpers.ScreenToWindow(window, 110, 220);

        Assert.Equal(10, x);
        Assert.Equal(20, y);
    }

    [Fact]
    public void RoundTrip_WindowScreenWindow()
    {
        var window = new WindowInfo
        {
            Handle = 1,
            Title = "Test",
            Bounds = new Rectangle(300, 150, 1024, 768),
        };

        var (screenX, screenY) = CoordinateHelpers.WindowToScreen(window, 50, 75);
        var (relX, relY) = CoordinateHelpers.ScreenToWindow(window, screenX, screenY);

        Assert.Equal(50, relX);
        Assert.Equal(75, relY);
    }

    [Fact]
    public void Center_Rectangle()
    {
        var rect = new Rectangle(10, 20, 100, 50);
        var (x, y) = CoordinateHelpers.Center(rect);

        Assert.Equal(60, x);
        Assert.Equal(45, y);
    }

    [Fact]
    public void Center_OcrWord()
    {
        var word = new OcrWord
        {
            Text = "hello",
            BoundingRect = new Rectangle(100, 200, 60, 20),
        };

        var (x, y) = CoordinateHelpers.Center(word);

        Assert.Equal(130, x);
        Assert.Equal(210, y);
    }

    [Fact]
    public void Center_OcrLine()
    {
        var line = new OcrLine
        {
            Text = "hello world",
            Words = [],
            BoundingRect = new Rectangle(50, 100, 200, 30),
        };

        var (x, y) = CoordinateHelpers.Center(line);

        Assert.Equal(150, x);
        Assert.Equal(115, y);
    }

    [Fact]
    public void Center_UIElement()
    {
        var element = new UIElement
        {
            Name = "Button1",
            BoundingRectangle = new Rectangle(400, 300, 120, 40),
        };

        var (x, y) = CoordinateHelpers.Center(element);

        Assert.Equal(460, x);
        Assert.Equal(320, y);
    }

    [Fact]
    public void Center_ZeroSizeRectangle_ReturnsOrigin()
    {
        var rect = new Rectangle(50, 50, 0, 0);
        var (x, y) = CoordinateHelpers.Center(rect);

        Assert.Equal(50, x);
        Assert.Equal(50, y);
    }
}
