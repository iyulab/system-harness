using SystemHarness.Windows;

namespace SystemHarness.Tests.Process;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class WindowsProcessManagerTests
{
    private readonly WindowsProcessManager _process = new();

    [Fact]
    public async Task ListAsync_ReturnsProcesses()
    {
        var processes = await _process.ListAsync();

        Assert.NotEmpty(processes);
        Assert.All(processes, p => Assert.True(p.Pid >= 0)); // PID 0 = Idle process on Windows
    }

    [Fact]
    public async Task ListAsync_WithFilter_FiltersResults()
    {
        var processes = await _process.ListAsync("explorer");

        Assert.All(processes, p =>
            Assert.Contains("explorer", p.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IsRunningAsync_ExistingProcess_ReturnsTrue()
    {
        var running = await _process.IsRunningAsync("explorer");

        Assert.True(running);
    }

    [Fact]
    public async Task IsRunningAsync_NonExistentProcess_ReturnsFalse()
    {
        var running = await _process.IsRunningAsync("nonexistent_process_xyz_12345");

        Assert.False(running);
    }

    [Fact]
    public async Task StartAsync_AndKill_WorksCorrectly()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();
        var info = await _process.StartAsync("notepad.exe");
        try
        {
            Assert.True(info.Pid > 0);
            Assert.Equal("notepad", info.Name, ignoreCase: true);

            // Give it a moment to start
            await Task.Delay(500);

            var isRunning = await _process.IsRunningAsync("notepad");
            Assert.True(isRunning);

            await _process.KillAsync(info.Pid);
            await Task.Delay(500);

            // Verify it's killed
            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(info.Pid);
                // If we get here, it hasn't exited yet — but HasExited might be true
                Assert.True(proc.HasExited);
                proc.Dispose();
            }
            catch (ArgumentException)
            {
                // Process already gone — expected
            }
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(info.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _process.KillAsync(info.Pid); } catch { }
        }
    }

    [Fact]
    public async Task KillAsync_NonExistentPid_DoesNotThrow()
    {
        // Should not throw even for non-existent PID
        await _process.KillAsync(999999);
    }

    [Fact]
    public async Task ProcessInfo_HasExpectedFields()
    {
        var processes = await _process.ListAsync("explorer");

        if (processes.Count > 0)
        {
            var info = processes[0];
            Assert.True(info.Pid > 0);
            Assert.NotNull(info.Name);
            Assert.NotEmpty(info.Name);
        }
    }
}
