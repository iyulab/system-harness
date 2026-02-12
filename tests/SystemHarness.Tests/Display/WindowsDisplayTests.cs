using SystemHarness.Windows;

namespace SystemHarness.Tests.Display;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class WindowsDisplayTests
{
    private readonly WindowsDisplay _display = new();

    [Fact]
    public async Task GetMonitorsAsync_ReturnsAtLeastOneMonitor()
    {
        var monitors = await _display.GetMonitorsAsync();

        Assert.NotEmpty(monitors);
        Assert.Contains(monitors, m => m.IsPrimary);
    }

    [Fact]
    public async Task GetMonitorsAsync_MonitorHasValidProperties()
    {
        var monitors = await _display.GetMonitorsAsync();
        var primary = monitors.First(m => m.IsPrimary);

        Assert.True(primary.Bounds.Width > 0);
        Assert.True(primary.Bounds.Height > 0);
        Assert.True(primary.DpiX >= 96);
        Assert.True(primary.DpiY >= 96);
        Assert.True(primary.ScaleFactor >= 1.0);
        Assert.NotEmpty(primary.Name);
        Assert.True(primary.Handle != 0);
    }

    [Fact]
    public async Task GetPrimaryMonitorAsync_ReturnsPrimary()
    {
        var primary = await _display.GetPrimaryMonitorAsync();

        Assert.True(primary.IsPrimary);
        Assert.True(primary.Bounds.Width > 0);
    }

    [Fact]
    public async Task GetMonitorAtPointAsync_ReturnsMonitor()
    {
        var primary = await _display.GetPrimaryMonitorAsync();
        var center = (primary.Bounds.X + primary.Bounds.Width / 2,
                      primary.Bounds.Y + primary.Bounds.Height / 2);

        var monitor = await _display.GetMonitorAtPointAsync(center.Item1, center.Item2);

        Assert.NotNull(monitor);
        Assert.True(monitor.Bounds.Width > 0);
    }

    [Fact]
    public async Task GetVirtualScreenBoundsAsync_ReturnsValidBounds()
    {
        var bounds = await _display.GetVirtualScreenBoundsAsync();

        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
    }

    [Fact]
    public async Task GetMonitorsAsync_WorkAreaSmallerThanBounds()
    {
        var monitors = await _display.GetMonitorsAsync();

        foreach (var monitor in monitors)
        {
            // Work area should be <= bounds (taskbar takes space)
            Assert.True(monitor.WorkArea.Width <= monitor.Bounds.Width);
            Assert.True(monitor.WorkArea.Height <= monitor.Bounds.Height);
        }
    }

    [Fact]
    public async Task GetMonitorsAsync_IndicesAreSequential()
    {
        var monitors = await _display.GetMonitorsAsync();

        for (var i = 0; i < monitors.Count; i++)
        {
            Assert.Equal(i, monitors[i].Index);
        }
    }

    [Fact]
    public async Task GetMonitorForWindowAsync_ReturnsMonitorForExistingWindow()
    {
        // Use any visible window title — the desktop window is always present
        var windows = new WindowsWindow();
        var list = await windows.ListAsync();

        if (list.Count == 0) return;

        var target = list[0];
        var monitor = await _display.GetMonitorForWindowAsync(target.Handle.ToString());

        Assert.NotNull(monitor);
        Assert.True(monitor.Bounds.Width > 0);
        Assert.True(monitor.Bounds.Height > 0);
    }
}
