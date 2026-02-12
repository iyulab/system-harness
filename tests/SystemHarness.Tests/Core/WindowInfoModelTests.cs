namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class WindowInfoModelTests
{
    [Fact]
    public void WindowInfo_RequiredProperties()
    {
        var info = new WindowInfo { Handle = 12345, Title = "Test Window" };

        Assert.Equal((nint)12345, info.Handle);
        Assert.Equal("Test Window", info.Title);
    }

    [Fact]
    public void WindowInfo_OptionalDefaults()
    {
        var info = new WindowInfo { Handle = 1, Title = "T" };

        Assert.Equal(0, info.ProcessId);
        Assert.False(info.IsVisible);
        Assert.Equal(default, info.Bounds);
        Assert.Null(info.ClassName);
        Assert.Null(info.ParentHandle);
        Assert.Equal(WindowState.Normal, info.State);
    }

    [Fact]
    public void WindowInfo_FullyPopulated()
    {
        var info = new WindowInfo
        {
            Handle = 99,
            Title = "Notepad",
            ProcessId = 1234,
            IsVisible = true,
            Bounds = new Rectangle(100, 200, 800, 600),
            ClassName = "Notepad",
            ParentHandle = 55,
            State = WindowState.Maximized,
        };

        Assert.Equal((nint)99, info.Handle);
        Assert.Equal("Notepad", info.Title);
        Assert.Equal(1234, info.ProcessId);
        Assert.True(info.IsVisible);
        Assert.Equal(800, info.Bounds.Width);
        Assert.Equal(600, info.Bounds.Height);
        Assert.Equal("Notepad", info.ClassName);
        Assert.Equal((nint)55, info.ParentHandle);
        Assert.Equal(WindowState.Maximized, info.State);
    }

    [Fact]
    public void WindowInfo_IsSealed()
    {
        Assert.True(typeof(WindowInfo).IsSealed);
    }
}
