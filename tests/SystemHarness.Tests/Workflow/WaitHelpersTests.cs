using SystemHarness.Windows;

namespace SystemHarness.Tests;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class WaitHelpersTests : IAsyncLifetime
{
    private readonly WindowsHarness _harness = new();
    private int _pid;
    private HashSet<nint> _handlesBefore = [];

    public async Task InitializeAsync()
    {
        _handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var info = await _harness.Process.StartAsync("notepad.exe");
        _pid = info.Pid;
        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        await NotepadHelper.CloseNotepadByPidAsync(_pid);
        await NotepadHelper.CloseNewNotepadWindowsAsync(_handlesBefore);
        _harness.Dispose();
    }

    [Fact]
    public async Task WaitForWindowStateAsync_Normal_ReturnsImmediately()
    {
        // Notepad starts in Normal state — should return immediately
        await WaitHelpers.WaitForWindowStateAsync(
            _harness, "Notepad", WindowState.Normal, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WaitForWindowStateAsync_Minimized_WaitsUntilMinimized()
    {
        // Minimize Notepad, then wait for it to reach Minimized state
        await _harness.Window.MinimizeAsync("Notepad");
        await WaitHelpers.WaitForWindowStateAsync(
            _harness, "Notepad", WindowState.Minimized, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task WaitForWindowStateAsync_Timeout_Throws()
    {
        // Notepad is Normal — waiting for Maximized with very short timeout should throw
        await Assert.ThrowsAsync<TimeoutException>(() =>
            WaitHelpers.WaitForWindowStateAsync(
                _harness, "Notepad", WindowState.Maximized, TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task WaitForWindowStateAsync_Cancellation_Throws()
    {
        using var cts = new CancellationTokenSource(200);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WaitHelpers.WaitForWindowStateAsync(
                _harness, "Notepad", WindowState.Maximized, TimeSpan.FromSeconds(30), cts.Token));
    }
}
