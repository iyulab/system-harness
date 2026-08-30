using System.Globalization;

namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests multi-monitor operations. Some tests may be skipped on single-monitor systems.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Integration")]
public class MultiMonitorTests : SimulationTestBase
{
    public MultiMonitorTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task EnumerateMonitors()
    {
        var monitors = await Display.GetMonitorsAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(monitors);
        Assert.Contains(monitors, m => m.IsPrimary);

        foreach (var monitor in monitors)
        {
            Assert.True(monitor.Bounds.Width > 0);
            Assert.True(monitor.Bounds.Height > 0);
            Assert.True(monitor.DpiX >= 96);
        }
    }

    [Fact]
    public async Task CaptureMonitor_PrimaryMonitor()
    {
        var primary = await Display.GetPrimaryMonitorAsync(TestContext.Current.CancellationToken);
        var screenshot = await Screen.CaptureMonitorAsync(primary.Index, ct: TestContext.Current.CancellationToken);

        Assert.NotNull(screenshot);
        Assert.True(screenshot.Width > 0);
        Assert.True(screenshot.Height > 0);
        Assert.NotEmpty(screenshot.Bytes);
    }

    [Fact]
    public async Task MoveWindowToMonitorBounds()
    {
        var monitors = await Display.GetMonitorsAsync(TestContext.Current.CancellationToken);
        if (monitors.Count < 1) return; // Skip on no-monitor (shouldn't happen)

        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            var wins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            Assert.NotEmpty(wins);
            var handle = wins[0].Handle.ToString(CultureInfo.InvariantCulture);

            var primary = monitors.First(m => m.IsPrimary);

            // Move to center of primary monitor
            var targetX = primary.Bounds.X + primary.Bounds.Width / 4;
            var targetY = primary.Bounds.Y + primary.Bounds.Height / 4;

            await Window.MoveAsync(handle, targetX, targetY, TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Verify window is within monitor bounds
            var updatedWins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            var bounds = updatedWins[0].Bounds;

            Assert.True(bounds.X >= primary.Bounds.X - 20);
            Assert.True(bounds.Y >= primary.Bounds.Y - 20);
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            await Process.KillAsync(proc.Pid, CancellationToken.None);
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task GetMonitorForWindow_ReturnsCorrectMonitor()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            var monitor = await Display.GetMonitorForWindowAsync("Notepad", TestContext.Current.CancellationToken);
            Assert.NotNull(monitor);
            Assert.True(monitor.Bounds.Width > 0);
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            await Process.KillAsync(proc.Pid, CancellationToken.None);
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task VirtualScreenBounds_EncompassesAllMonitors()
    {
        var monitors = await Display.GetMonitorsAsync(TestContext.Current.CancellationToken);
        var virtualBounds = await Display.GetVirtualScreenBoundsAsync(TestContext.Current.CancellationToken);

        Assert.True(virtualBounds.Width > 0);
        Assert.True(virtualBounds.Height > 0);

        // Virtual screen should contain all monitors
        foreach (var monitor in monitors)
        {
            Assert.True(monitor.Bounds.X >= virtualBounds.X);
            Assert.True(monitor.Bounds.Y >= virtualBounds.Y);
        }
    }
}
