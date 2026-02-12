using SystemHarness.SimulationTests.Helpers;

namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests application lifecycle: start → interact → graceful close.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Local")]
public class AppLifecycleTests : SimulationTestBase
{
    public AppLifecycleTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Notepad_StartTypeAndClose()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            await Keyboard.TypeAsync("Hello from Simulation Test!");
            await Task.Delay(500);

            // Verify window exists
            var windows = await Window.FindByProcessIdAsync(proc.Pid);
            Assert.NotEmpty(windows);

            // Close without saving (Alt+F4 then Don't Save)
            await Keyboard.HotkeyAsync(default, Key.Alt, Key.F4);
            await Task.Delay(500);

            // Handle "Do you want to save" dialog — press Don't Save (Tab, Enter or 'N')
            await Keyboard.KeyPressAsync(Key.Tab);
            await Task.Delay(100);
            await Keyboard.KeyPressAsync(Key.Enter);
        }
        finally
        {
            try { await Process.KillAsync(proc.Pid); } catch { }
        }
    }

    [Fact]
    public async Task Notepad_StartWithProcessStartOptions()
    {
        var tempDir = Path.GetTempPath();
        var options = new ProcessStartOptions
        {
            WorkingDirectory = tempDir,
        };

        var proc = await Process.StartAsync("notepad.exe", options);
        await Task.Delay(1000);

        try
        {
            Assert.True(proc.Pid > 0);
            var running = await Process.IsRunningAsync("notepad");
            Assert.True(running);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task Process_WaitForExit_ShortLivedProcess()
    {
        var options = new ProcessStartOptions
        {
            Arguments = "/c echo hello",
            RedirectOutput = true,
        };

        var proc = await Process.StartAsync("cmd.exe", options);
        var exited = await Process.WaitForExitAsync(proc.Pid, TimeSpan.FromSeconds(5));

        Assert.True(exited);
    }

    [Fact]
    public async Task GracefulShutdown_ClosesApplication()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        await AppLifecycleHelper.GracefulShutdownAsync(
            Process, Window, "Notepad", proc.Pid,
            TimeSpan.FromSeconds(3));

        await Task.Delay(500);

        // Verify process is gone
        try
        {
            var p = System.Diagnostics.Process.GetProcessById(proc.Pid);
            Assert.True(p.HasExited);
        }
        catch (ArgumentException)
        {
            // Expected — process exited
        }
    }
}
