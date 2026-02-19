using SystemHarness;
using SystemHarness.Mcp;

namespace SystemHarness.Tests.Mcp;

[Collection("StaticState")]
[Trait("Category", "CI")]
public class SafeZoneTests : IDisposable
{
    public SafeZoneTests() => SafeZone.Clear();
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        SafeZone.Clear();
    }

    // --- Current ---

    [Fact]
    public void Current_Default_IsNull()
    {
        Assert.Null(SafeZone.Current);
    }

    // --- Set ---

    [Fact]
    public void Set_WindowOnly_SetsCurrent()
    {
        SafeZone.Set("Notepad");

        var current = SafeZone.Current;
        Assert.NotNull(current);
        Assert.Equal("Notepad", current.Window);
        Assert.Null(current.Region);
    }

    [Fact]
    public void Set_WithRegion_SetsBoth()
    {
        var region = new Rectangle(10, 20, 300, 400);
        SafeZone.Set("Calculator", region);

        var current = SafeZone.Current;
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
        SafeZone.Set("Notepad");
        SafeZone.Set("Calculator");

        Assert.Equal("Calculator", SafeZone.Current!.Window);
    }

    // --- Clear ---

    [Fact]
    public void Clear_RemovesSafeZone()
    {
        SafeZone.Set("Notepad");
        Assert.NotNull(SafeZone.Current);

        SafeZone.Clear();
        Assert.Null(SafeZone.Current);
    }

    [Fact]
    public void Clear_WhenAlreadyNull_NoError()
    {
        SafeZone.Clear();
        SafeZone.Clear();
        Assert.Null(SafeZone.Current);
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
