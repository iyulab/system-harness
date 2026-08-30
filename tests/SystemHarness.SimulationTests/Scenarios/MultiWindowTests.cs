using System.Globalization;

namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests multi-window operations: focus switching, minimize/maximize/restore, positioning.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Integration")]
public class MultiWindowTests : SimulationTestBase
{
    public MultiWindowTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task TwoNotepads_IndependentTextInput()
    {
        var proc1 = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        var proc2 = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            // Find windows for each process
            var wins1 = await Window.FindByProcessIdAsync(proc1.Pid, TestContext.Current.CancellationToken);
            var wins2 = await Window.FindByProcessIdAsync(proc2.Pid, TestContext.Current.CancellationToken);

            Assert.NotEmpty(wins1);
            Assert.NotEmpty(wins2);

            // Type in first notepad
            await Window.FocusAsync(wins1[0].Handle.ToString(CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);
            await Keyboard.TypeAsync("Text for Notepad 1", ct: TestContext.Current.CancellationToken);

            // Type in second notepad
            await Window.FocusAsync(wins2[0].Handle.ToString(CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);
            await Keyboard.TypeAsync("Text for Notepad 2", ct: TestContext.Current.CancellationToken);

            // Verify each has its own content
            await Window.FocusAsync(wins1[0].Handle.ToString(CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.A });
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.C });
            await Task.Delay(200, TestContext.Current.CancellationToken);
            var text1 = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);

            await Window.FocusAsync(wins2[0].Handle.ToString(CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.A });
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.C });
            await Task.Delay(200, TestContext.Current.CancellationToken);
            var text2 = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);

            Assert.Contains("Notepad 1", text1 ?? "");
            Assert.Contains("Notepad 2", text2 ?? "");
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            await Process.KillAsync(proc1.Pid, CancellationToken.None);
            await Process.KillAsync(proc2.Pid, CancellationToken.None);
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task MinimizeMaximizeRestore_Cycle()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            var wins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            Assert.NotEmpty(wins);
            var handle = wins[0].Handle.ToString(CultureInfo.InvariantCulture);

            // Normal → Maximize
            await Window.MaximizeAsync(handle, TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);
            var state = await Window.GetStateAsync(handle, TestContext.Current.CancellationToken);
            Assert.Equal(WindowState.Maximized, state);

            // Maximize → Minimize
            await Window.MinimizeAsync(handle, TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);
            state = await Window.GetStateAsync(handle, TestContext.Current.CancellationToken);
            Assert.Equal(WindowState.Minimized, state);

            // Minimize → Restore
            await Window.RestoreAsync(handle, TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);
            state = await Window.GetStateAsync(handle, TestContext.Current.CancellationToken);
            Assert.Equal(WindowState.Normal, state);
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
    public async Task MoveWindow_VerifyBounds()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            var wins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            Assert.NotEmpty(wins);
            var handle = wins[0].Handle.ToString(CultureInfo.InvariantCulture);

            // Move to specific position
            await Window.MoveAsync(handle, 100, 100, TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Get updated window info and check bounds
            var updatedWins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            var bounds = updatedWins[0].Bounds;

            // Allow some tolerance for window decorations
            Assert.InRange(bounds.X, 95, 110);
            Assert.InRange(bounds.Y, 95, 110);
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
    public async Task ResizeWindow_VerifyDimensions()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            var wins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            Assert.NotEmpty(wins);
            var handle = wins[0].Handle.ToString(CultureInfo.InvariantCulture);

            await Window.ResizeAsync(handle, 800, 600, TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            var updatedWins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            var bounds = updatedWins[0].Bounds;

            // Allow some tolerance
            Assert.InRange(bounds.Width, 795, 810);
            Assert.InRange(bounds.Height, 595, 610);
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            await Process.KillAsync(proc.Pid, CancellationToken.None);
            #pragma warning restore xUnit1051
        }
    }
}
