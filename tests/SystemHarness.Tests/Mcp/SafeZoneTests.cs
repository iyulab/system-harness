using SystemHarness;
using SystemHarness.Mcp;

namespace SystemHarness.Tests.Mcp;

[Collection("StaticState")]
[Trait("Category", "CI")]
public class SafeZoneTests : IDisposable
{
    private readonly SafeZone _zone = new();

    public SafeZoneTests() => _zone.Clear();
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _zone.Clear();
    }

    // --- Current ---

    [Fact]
    public void Current_Default_IsNull()
    {
        Assert.Null(_zone.Current);
    }

    // --- Set ---

    [Fact]
    public void Set_WindowOnly_SetsCurrent()
    {
        _zone.Set("Notepad");

        var current = _zone.Current;
        Assert.NotNull(current);
        Assert.Equal("Notepad", current.Window);
        Assert.Null(current.Region);
    }

    [Fact]
    public void Set_WithRegion_SetsBoth()
    {
        var region = new Rectangle(10, 20, 300, 400);
        _zone.Set("Calculator", region);

        var current = _zone.Current;
        Assert.NotNull(current);
        Assert.Equal("Calculator", current.Window);
        Assert.NotNull(current.Region);
        Assert.Equal(10, current.Region.Value.X);
        Assert.Equal(20, current.Region.Value.Y);
        Assert.Equal(300, current.Region.Value.Width);
        Assert.Equal(400, current.Region.Value.Height);
    }

    [Fact]
    public void Set_OverwritesPrevious()
    {
        _zone.Set("Notepad");
        _zone.Set("Calculator");

        Assert.Equal("Calculator", _zone.Current!.Window);
    }

    // --- Clear ---

    [Fact]
    public void Clear_RemovesSafeZone()
    {
        _zone.Set("Notepad");
        Assert.NotNull(_zone.Current);

        _zone.Clear();
        Assert.Null(_zone.Current);
    }

    [Fact]
    public void Clear_WhenAlreadyNull_NoError()
    {
        _zone.Clear();
        _zone.Clear();
        Assert.Null(_zone.Current);
    }

    // --- SafeZoneConfig record ---

    [Fact]
    public void SafeZoneConfig_Equality()
    {
        var region = new Rectangle(0, 0, 100, 100);
        var a = new SafeZoneConfig("Window1", region);
        var b = new SafeZoneConfig("Window1", region);

        Assert.Equal(a, b);
    }

    [Fact]
    public void SafeZoneConfig_Inequality_DifferentWindow()
    {
        var a = new SafeZoneConfig("Window1", null);
        var b = new SafeZoneConfig("Window2", null);

        Assert.NotEqual(a, b);
    }
}
