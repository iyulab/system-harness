namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class DisplayModelTests
{
    [Fact]
    public void MonitorInfo_RequiredProperties()
    {
        var info = new MonitorInfo
        {
            Index = 0,
            Name = @"\\.\DISPLAY1",
            Bounds = new Rectangle(0, 0, 1920, 1080),
        };

        Assert.Equal(0, info.Index);
        Assert.Equal(@"\\.\DISPLAY1", info.Name);
        Assert.Equal(new Rectangle(0, 0, 1920, 1080), info.Bounds);
    }

    [Fact]
    public void MonitorInfo_OptionalProperties_Defaults()
    {
        var info = new MonitorInfo
        {
            Index = 0,
            Name = "test",
            Bounds = new Rectangle(0, 0, 100, 100),
        };

        Assert.Equal(default, info.WorkArea);
        Assert.False(info.IsPrimary);
        Assert.Equal(0.0, info.DpiX);
        Assert.Equal(0.0, info.DpiY);
        Assert.Equal(0.0, info.ScaleFactor);
        Assert.Equal(nint.Zero, info.Handle);
    }

    [Fact]
    public void MonitorInfo_FullyPopulated()
    {
        var info = new MonitorInfo
        {
            Index = 1,
            Name = @"\\.\DISPLAY2",
            Bounds = new Rectangle(1920, 0, 2560, 1440),
            WorkArea = new Rectangle(1920, 0, 2560, 1400),
            IsPrimary = false,
            DpiX = 109.0,
            DpiY = 109.0,
            ScaleFactor = 1.25,
            Handle = 12345,
        };

        Assert.Equal(1, info.Index);
        Assert.False(info.IsPrimary);
        Assert.Equal(109.0, info.DpiX);
        Assert.Equal(1.25, info.ScaleFactor);
        Assert.Equal(12345, info.Handle);
    }

    [Fact]
    public void MonitorInfo_PrimaryDisplay()
    {
        var info = new MonitorInfo
        {
            Index = 0,
            Name = @"\\.\DISPLAY1",
            Bounds = new Rectangle(0, 0, 3840, 2160),
            WorkArea = new Rectangle(0, 0, 3840, 2112),
            IsPrimary = true,
            DpiX = 163.0,
            DpiY = 163.0,
            ScaleFactor = 2.0,
        };

        Assert.True(info.IsPrimary);
        Assert.Equal(2.0, info.ScaleFactor);
    }
}
