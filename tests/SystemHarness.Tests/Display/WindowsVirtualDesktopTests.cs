using SystemHarness.Windows;

namespace SystemHarness.Tests.Display;

[Trait("Category", "CI")]
public class WindowsVirtualDesktopTests
{
    private readonly WindowsVirtualDesktop _vd = new();

    [Fact]
    public async Task GetDesktopCountAsync_ThrowsHarnessException()
    {
        var ex = await Assert.ThrowsAsync<HarnessException>(
            () => _vd.GetDesktopCountAsync());
        Assert.Contains("undocumented COM", ex.Message);
    }

    [Fact]
    public async Task GetCurrentDesktopIndexAsync_ThrowsHarnessException()
    {
        var ex = await Assert.ThrowsAsync<HarnessException>(
            () => _vd.GetCurrentDesktopIndexAsync());
        Assert.Contains("undocumented COM", ex.Message);
    }

    [Fact]
    public async Task MoveWindowToDesktopAsync_ThrowsHarnessException()
    {
        var ex = await Assert.ThrowsAsync<HarnessException>(
            () => _vd.MoveWindowToDesktopAsync("Notepad", 1));
        Assert.Contains("IVirtualDesktopManager", ex.Message);
    }

    [Fact]
    public async Task SwitchToDesktopAsync_RespectsCancel()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _vd.SwitchToDesktopAsync(1, cts.Token));
    }
}
