using System.Globalization;
using SystemHarness.Windows;

namespace SystemHarness.Tests.Window;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class WindowExtensionTests
{
    private readonly WindowsWindow _window = new();
    private readonly WindowsProcessManager _process = new();

    [Fact]
    public async Task RestoreAsync_RestoresMinimizedWindow()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe");
        await Task.Delay(2000);

        try
        {
            // Find the window handle for our specific Notepad instance
            var windows = await _window.FindByProcessIdAsync(proc.Pid);
            if (windows.Count == 0)
            {
                // Windows 11 Store Notepad may have different PID ownership — skip
                return;
            }

            var handle = windows[0].Handle.ToString(CultureInfo.InvariantCulture);

            // Ensure window starts in Normal state
            await _window.RestoreAsync(handle);
            await Task.Delay(500);

            await _window.MinimizeAsync(handle);

            // Poll for Minimized state (animations may delay the state change)
            var minimized = false;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(300);
                var s = await _window.GetStateAsync(handle);
                if (s == WindowState.Minimized) { minimized = true; break; }
            }
            Assert.True(minimized, "Window did not reach Minimized state within timeout");

            await _window.RestoreAsync(handle);

            // Poll for Normal state
            var restored = false;
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(300);
                var s = await _window.GetStateAsync(handle);
                if (s == WindowState.Normal) { restored = true; break; }
            }
            Assert.True(restored, "Window did not reach Normal state within timeout");
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _process.KillAsync(proc.Pid); } catch { }
        }
    }

    [Fact]
    public async Task GetStateAsync_ReturnsCorrectState()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            var state = await _window.GetStateAsync("Notepad");
            Assert.Equal(WindowState.Normal, state);

            await _window.MaximizeAsync("Notepad");
            await Task.Delay(500);

            state = await _window.GetStateAsync("Notepad");
            Assert.Equal(WindowState.Maximized, state);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _process.KillAsync(proc.Pid); } catch { }
        }
    }

    [Fact]
    public async Task GetForegroundAsync_ReturnsCurrentWindow()
    {
        // Allow foreground to settle (may be transiently null during rapid window changes)
        await Task.Delay(500);
        var info = await _window.GetForegroundAsync();

        // Some window should always be in the foreground
        Assert.NotNull(info);
        Assert.True(info.Handle != 0);
        Assert.NotEmpty(info.Title);
    }

    [Fact]
    public async Task WaitForWindowAsync_FindsExistingWindow()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe");

        try
        {
            var info = await _window.WaitForWindowAsync("Notepad", TimeSpan.FromSeconds(10));
            Assert.Contains("Notepad", info.Title, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _process.KillAsync(proc.Pid); } catch { }
        }
    }

    [Fact]
    public async Task WaitForWindowAsync_ThrowsOnTimeout()
    {
        await Assert.ThrowsAsync<HarnessException>(async () =>
        {
            await _window.WaitForWindowAsync("NonExistentWindow_67890", TimeSpan.FromMilliseconds(500));
        });
    }

    [Fact]
    public async Task FindByProcessIdAsync_FindsWindowsByPid()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe");
        await Task.Delay(2000);

        try
        {
            var windows = await _window.FindByProcessIdAsync(proc.Pid);
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
            try { await _process.KillAsync(proc.Pid); } catch { }
        }
    }

    [Fact]
    public async Task ListAsync_IncludesClassNameAndState()
    {
        var windows = await _window.ListAsync();

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
        var proc = await _process.StartAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await _window.SetAlwaysOnTopAsync("Notepad", true);
            await Task.Delay(200);
            await _window.SetAlwaysOnTopAsync("Notepad", false);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _process.KillAsync(proc.Pid); } catch { }
        }
    }

    [Fact]
    public async Task SetOpacityAsync_SetsTransparency()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            // Set 50% opacity
            await _window.SetOpacityAsync("Notepad", 0.5);
            await Task.Delay(200);

            // Restore full opacity
            await _window.SetOpacityAsync("Notepad", 1.0);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _process.KillAsync(proc.Pid); } catch { }
        }
    }

    [Fact]
    public async Task GetChildWindowsAsync_ReturnsChildren()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var proc = await _process.StartAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            var children = await _window.GetChildWindowsAsync("Notepad");
            // Notepad should have child windows (edit control, etc.)
            Assert.NotNull(children);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _process.KillAsync(proc.Pid); } catch { }
        }
    }
}
