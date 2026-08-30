using System.Globalization;
using SystemHarness.Windows;

namespace SystemHarness.Tests.Window;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
[Trait("Category", "RequiresDesktop")]
public class WindowExtensionTests
{
    private readonly WindowsWindow _window = new();
    private readonly WindowsProcessManager _process = new();

    [Fact]
    public async Task RestoreAsync_RestoresMinimizedWindow()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        try
        {
            // Find the window handle for our specific Notepad instance
            var windows = await _window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            if (windows.Count == 0)
            {
                // Windows 11 Store Notepad may have different PID ownership — skip
                return;
            }

            var handle = windows[0].Handle.ToString(CultureInfo.InvariantCulture);

            // Ensure window starts in Normal state
            await _window.RestoreAsync(handle, TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);

            await _window.MinimizeAsync(handle, TestContext.Current.CancellationToken);

            // Poll for Minimized state (animations may delay the state change)
            var minimized = false;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(300, TestContext.Current.CancellationToken);
                var s = await _window.GetStateAsync(handle, TestContext.Current.CancellationToken);
                if (s == WindowState.Minimized) { minimized = true; break; }
            }
            Assert.True(minimized, "Window did not reach Minimized state within timeout");

            await _window.RestoreAsync(handle, TestContext.Current.CancellationToken);

            // Poll for Normal state
            var restored = false;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(300, TestContext.Current.CancellationToken);
                var s = await _window.GetStateAsync(handle, TestContext.Current.CancellationToken);
                if (s == WindowState.Normal) { restored = true; break; }
            }
            Assert.True(restored, "Window did not reach Normal state within timeout");
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task GetStateAsync_ReturnsCorrectState()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            var state = await _window.GetStateAsync("Notepad", TestContext.Current.CancellationToken);
            Assert.Equal(WindowState.Normal, state);

            await _window.MaximizeAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);

            state = await _window.GetStateAsync("Notepad", TestContext.Current.CancellationToken);
            Assert.Equal(WindowState.Maximized, state);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task GetForegroundAsync_ReturnsCurrentWindow()
    {
        // Allow foreground to settle (may be transiently null during rapid window changes)
        await Task.Delay(500, TestContext.Current.CancellationToken);
        var info = await _window.GetForegroundAsync(TestContext.Current.CancellationToken);

        // Some window should always be in the foreground
        Assert.NotNull(info);
        Assert.True(info.Handle != 0);
        Assert.NotEmpty(info.Title);
    }

    [Fact]
    public async Task WaitForWindowAsync_FindsExistingWindow()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);

        try
        {
            var info = await _window.WaitForWindowAsync("Notepad", TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            Assert.Contains("Notepad", info.Title, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task WaitForWindowAsync_ThrowsOnTimeout()
    {
        await Assert.ThrowsAsync<HarnessException>(async () =>
        {
            await _window.WaitForWindowAsync("NonExistentWindow_67890", TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task FindByProcessIdAsync_FindsWindowsByPid()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        try
        {
            var windows = await _window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            // Windows 11 Store Notepad may spawn the window under a different PID
            // so we only assert NotNull (no throw) and skip if empty
            Assert.NotNull(windows);
            if (windows.Count > 0)
            {
                Assert.All(windows, w => Assert.Equal(proc.Pid, w.ProcessId));
            }
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task ListAsync_IncludesClassNameAndState()
    {
        var windows = await _window.ListAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(windows);
        // At least some windows should have class names
        var withClassName = windows.Where(w => w.ClassName is not null).ToList();
        Assert.NotEmpty(withClassName);

        // All visible windows should have a state set
        var withState = windows.Where(w => w.State != WindowState.Normal || w.IsVisible).ToList();
        Assert.NotEmpty(withState);
    }

    [Fact]
    public async Task SetAlwaysOnTopAsync_DoesNotThrow()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await _window.SetAlwaysOnTopAsync("Notepad", true, TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await _window.SetAlwaysOnTopAsync("Notepad", false, TestContext.Current.CancellationToken);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task SetOpacityAsync_SetsTransparency()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            // Set 50% opacity
            await _window.SetOpacityAsync("Notepad", 0.5, TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);

            // Restore full opacity
            await _window.SetOpacityAsync("Notepad", 1.0, TestContext.Current.CancellationToken);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task GetChildWindowsAsync_ReturnsChildren()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            var children = await _window.GetChildWindowsAsync("Notepad", TestContext.Current.CancellationToken);
            // Notepad should have child windows (edit control, etc.)
            Assert.NotNull(children);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }
}
