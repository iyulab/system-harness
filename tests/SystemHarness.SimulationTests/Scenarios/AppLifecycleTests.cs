using SystemHarness.SimulationTests.Helpers;

namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests application lifecycle: start → interact → graceful close.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Integration")]
public class AppLifecycleTests : SimulationTestBase
{
    public AppLifecycleTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Notepad_StartTypeAndClose()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            await Keyboard.TypeAsync("Hello from Simulation Test!", ct: TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);

            // Verify window exists
            var windows = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            Assert.NotEmpty(windows);

            // Close without saving (Alt+F4 then Don't Save)
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Alt, Key.F4 });
            await Task.Delay(500, TestContext.Current.CancellationToken);

            // Handle "Do you want to save" dialog — press Don't Save (Tab, Enter or 'N')
            await Keyboard.KeyPressAsync(Key.Tab, TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await Keyboard.KeyPressAsync(Key.Enter, TestContext.Current.CancellationToken);
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is
            // exactly when this kill matters most.
#pragma warning disable xUnit1051
            try { await Process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
#pragma warning restore xUnit1051
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

        var proc = await Process.StartAsync("notepad.exe", options, TestContext.Current.CancellationToken);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            Assert.True(proc.Pid > 0);
            var running = await Process.IsRunningAsync("notepad", TestContext.Current.CancellationToken);
            Assert.True(running);
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is
            // exactly when this kill matters most.
#pragma warning disable xUnit1051
            await Process.KillAsync(proc.Pid, CancellationToken.None);
#pragma warning restore xUnit1051
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

        var proc = await Process.StartAsync("cmd.exe", options, TestContext.Current.CancellationToken);
        var exited = await Process.WaitForExitAsync(proc.Pid, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(exited);
    }

    [Fact]
    public async Task GracefulShutdown_ClosesApplication()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        await AppLifecycleHelper.GracefulShutdownAsync(
            Process, Window, "Notepad", proc.Pid,
            TimeSpan.FromSeconds(3));

        await Task.Delay(500, TestContext.Current.CancellationToken);

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
