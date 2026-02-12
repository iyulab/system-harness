namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests multi-window operations: focus switching, minimize/maximize/restore, positioning.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Local")]
public class MultiWindowTests : SimulationTestBase
{
    public MultiWindowTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task TwoNotepads_IndependentTextInput()
    {
        var proc1 = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);
        var proc2 = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            // Find windows for each process
            var wins1 = await Window.FindByProcessIdAsync(proc1.Pid);
            var wins2 = await Window.FindByProcessIdAsync(proc2.Pid);

            Assert.NotEmpty(wins1);
            Assert.NotEmpty(wins2);

            // Type in first notepad
            await Window.FocusAsync(wins1[0].Handle.ToString());
            await Task.Delay(300);
            await Keyboard.TypeAsync("Text for Notepad 1");

            // Type in second notepad
            await Window.FocusAsync(wins2[0].Handle.ToString());
            await Task.Delay(300);
            await Keyboard.TypeAsync("Text for Notepad 2");

            // Verify each has its own content
            await Window.FocusAsync(wins1[0].Handle.ToString());
            await Task.Delay(200);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
            await Task.Delay(100);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.C);
            await Task.Delay(200);
            var text1 = await Clipboard.GetTextAsync();

            await Window.FocusAsync(wins2[0].Handle.ToString());
            await Task.Delay(200);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
            await Task.Delay(100);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.C);
            await Task.Delay(200);
            var text2 = await Clipboard.GetTextAsync();

            Assert.Contains("Notepad 1", text1 ?? "");
            Assert.Contains("Notepad 2", text2 ?? "");
        }
        finally
        {
            await Process.KillAsync(proc1.Pid);
            await Process.KillAsync(proc2.Pid);
        }
    }

    [Fact]
    public async Task MinimizeMaximizeRestore_Cycle()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            var wins = await Window.FindByProcessIdAsync(proc.Pid);
            Assert.NotEmpty(wins);
            var handle = wins[0].Handle.ToString();

            // Normal → Maximize
            await Window.MaximizeAsync(handle);
            await Task.Delay(500);
            var state = await Window.GetStateAsync(handle);
            Assert.Equal(WindowState.Maximized, state);

            // Maximize → Minimize
            await Window.MinimizeAsync(handle);
            await Task.Delay(500);
            state = await Window.GetStateAsync(handle);
            Assert.Equal(WindowState.Minimized, state);

            // Minimize → Restore
            await Window.RestoreAsync(handle);
            await Task.Delay(500);
            state = await Window.GetStateAsync(handle);
            Assert.Equal(WindowState.Normal, state);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task MoveWindow_VerifyBounds()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            var wins = await Window.FindByProcessIdAsync(proc.Pid);
            Assert.NotEmpty(wins);
            var handle = wins[0].Handle.ToString();

            // Move to specific position
            await Window.MoveAsync(handle, 100, 100);
            await Task.Delay(300);

            // Get updated window info and check bounds
            var updatedWins = await Window.FindByProcessIdAsync(proc.Pid);
            var bounds = updatedWins[0].Bounds;

            // Allow some tolerance for window decorations
            Assert.InRange(bounds.X, 95, 110);
            Assert.InRange(bounds.Y, 95, 110);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task ResizeWindow_VerifyDimensions()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            var wins = await Window.FindByProcessIdAsync(proc.Pid);
            Assert.NotEmpty(wins);
            var handle = wins[0].Handle.ToString();

            await Window.ResizeAsync(handle, 800, 600);
            await Task.Delay(300);

            var updatedWins = await Window.FindByProcessIdAsync(proc.Pid);
            var bounds = updatedWins[0].Bounds;

            // Allow some tolerance
            Assert.InRange(bounds.Width, 795, 810);
            Assert.InRange(bounds.Height, 595, 610);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }
}
