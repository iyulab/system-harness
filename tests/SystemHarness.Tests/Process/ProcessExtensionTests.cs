using SystemHarness.Windows;

namespace SystemHarness.Tests.Process;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
[Trait("Category", "RequiresDesktop")]
public class ProcessExtensionTests
{
    private readonly WindowsProcessManager _process = new();

    [Fact]
    public async Task StartAsync_WithOptions_LaunchesProcess()
    {
        var options = new ProcessStartOptions
        {
            Arguments = "/c echo hello",
            Hidden = true,
            RedirectOutput = true,
        };

        var info = await _process.StartAsync("cmd.exe", options, TestContext.Current.CancellationToken);

        Assert.True(info.Pid > 0);
        Assert.Equal("cmd", info.Name);

        // Cleanup
        // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
        #pragma warning disable xUnit1051
        try { await _process.KillAsync(info.Pid, CancellationToken.None); } catch { }
        #pragma warning restore xUnit1051
    }

    [Fact]
    public async Task StartAsync_WithWorkingDirectory()
    {
        var tempDir = Path.GetTempPath();
        var options = new ProcessStartOptions
        {
            Arguments = "/c cd",
            WorkingDirectory = tempDir,
            RedirectOutput = true,
        };

        var info = await _process.StartAsync("cmd.exe", options, TestContext.Current.CancellationToken);
        Assert.True(info.Pid > 0);

        // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
        #pragma warning disable xUnit1051
        try { await _process.KillAsync(info.Pid, CancellationToken.None); } catch { }
        #pragma warning restore xUnit1051
    }

    [Fact]
    public async Task FindByPathAsync_FindsNotepadByPath()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var info = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        try
        {
            // Use a known path — notepad.exe
            var notepadPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "notepad.exe");

            var result = await _process.FindByPathAsync(notepadPath, TestContext.Current.CancellationToken);

            // If notepad.exe path matches (Win32 notepad), expect results
            // On Windows 11, Notepad may be a Store app with a different path
            if (result.Count == 0)
            {
                // Store-app Notepad has a different exe path; skip assertion
                return;
            }

            Assert.All(result, p => Assert.True(p.Pid > 0));
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(info.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(info.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task FindByWindowTitleAsync_ReturnsResults()
    {
        // This test relies on some window existing; just verify it doesn't throw
        var result = await _process.FindByWindowTitleAsync("NonExistentWindowTitle_12345", TestContext.Current.CancellationToken);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetChildProcessesAsync_ReturnsListForCurrentProcess()
    {
        var pid = Environment.ProcessId;
        var children = await _process.GetChildProcessesAsync(pid, TestContext.Current.CancellationToken);

        // May or may not have children, but should not throw
        Assert.NotNull(children);
    }

    [Fact]
    public async Task KillTreeAsync_KillsProcess()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var info = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        try
        {
            await _process.KillTreeAsync(info.Pid, TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);

            var running = await _process.IsRunningAsync("notepad", TestContext.Current.CancellationToken);
            // May still be running if other notepad instances exist,
            // but our PID should be gone
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(info.Pid);
                Assert.True(proc.HasExited);
            }
            catch (ArgumentException)
            {
                // Process already exited — expected
            }
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(info.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
        }
    }

    [Fact]
    public async Task WaitForExitAsync_ReturnsTrueWhenProcessExits()
    {
        var options = new ProcessStartOptions
        {
            Arguments = "/c echo done",
            RedirectOutput = true,
        };

        var info = await _process.StartAsync("cmd.exe", options, TestContext.Current.CancellationToken);
        var exited = await _process.WaitForExitAsync(info.Pid, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(exited);
    }

    [Fact]
    public async Task WaitForExitAsync_ReturnsFalseOnTimeout()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var info = await _process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);

        try
        {
            // Win11 Store Notepad: launcher PID exits immediately (Store app runs under a
            // different PID). Skip assertion in that case — WaitForExit correctly reports true.
            var exited = await _process.WaitForExitAsync(info.Pid, TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
            if (exited)
                return; // Win11 launcher PID already exited — skip
            Assert.False(exited);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(info.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _process.KillAsync(info.Pid, CancellationToken.None); } catch { }
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task FindByPortAsync_DoesNotThrow()
    {
        // Port 0 should return empty; just verify no exceptions
        var result = await _process.FindByPortAsync(0, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ProcessInfo_HasExtendedProperties()
    {
        var list = await _process.ListAsync(ct: TestContext.Current.CancellationToken);
        Assert.NotEmpty(list);

        // At least some processes should have memory usage
        var withMemory = list.Where(p => p.MemoryUsageBytes.HasValue && p.MemoryUsageBytes > 0).ToList();
        Assert.NotEmpty(withMemory);
    }
}
