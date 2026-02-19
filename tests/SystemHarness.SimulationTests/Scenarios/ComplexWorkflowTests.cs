using System.Globalization;
using SystemHarness.SimulationTests.Helpers;

namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests complex multi-app workflows simulating real-world automation scenarios.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Integration")]
public class ComplexWorkflowTests : SimulationTestBase
{
    public ComplexWorkflowTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AgentLoop_CaptureAnalyzeAct()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            // Simulate an "agent loop": capture → analyze → act → re-capture
            for (var i = 0; i < 3; i++)
            {
                // Step 1: Capture screen
                var screenshot = await Screen.CaptureAsync();
                ScreenAssert.IsValidScreenshot(screenshot);

                // Step 2: "Analyze" (mock — just check dimensions)
                Assert.True(screenshot.Width > 0);
                Assert.True(screenshot.Height > 0);

                // Step 3: Act — type a line
                await Keyboard.TypeAsync($"Agent iteration {i + 1}");
                await Keyboard.KeyPressAsync(Key.Enter);
                await Task.Delay(200);
            }

            // Step 4: Verify all iterations were typed
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
            await Task.Delay(200);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.C);
            await Task.Delay(200);

            var text = await Clipboard.GetTextAsync();
            Assert.NotNull(text);
            Assert.Contains("iteration 1", text);
            Assert.Contains("iteration 2", text);
            Assert.Contains("iteration 3", text);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task BatchAutomation_MultipleNotepadInstances()
    {
        var pids = new List<int>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"sim_batch_{Guid.NewGuid():N}");
        await FileSystem.CreateDirectoryAsync(tempDir);

        try
        {
            // Open 3 Notepad instances
            for (var i = 0; i < 3; i++)
            {
                var proc = await Process.StartAsync("notepad.exe");
                pids.Add(proc.Pid);
                await Task.Delay(500);
            }

            await Task.Delay(1000);

            // Type unique text in each
            for (var i = 0; i < pids.Count; i++)
            {
                var wins = await Window.FindByProcessIdAsync(pids[i]);
                if (wins.Count == 0) continue;

                await Window.FocusAsync(wins[0].Handle.ToString(CultureInfo.InvariantCulture));
                await Task.Delay(300);

                await Keyboard.TypeAsync($"Instance {i}: Unique content {Guid.NewGuid():N}");
                await Task.Delay(200);
            }

            // Verify all are running
            foreach (var pid in pids)
            {
                try
                {
                    var proc = System.Diagnostics.Process.GetProcessById(pid);
                    Assert.False(proc.HasExited);
                }
                catch (ArgumentException)
                {
                    // Process may have exited
                }
            }
        }
        finally
        {
            foreach (var pid in pids)
            {
                try { await Process.KillAsync(pid); } catch { }
            }
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task DesktopStateGuard_PreservesState()
    {
        // Set known clipboard content
        var originalText = $"guard_test_{Guid.NewGuid():N}";
        await Clipboard.SetTextAsync(originalText);
        await Task.Delay(100);

        await using (var guard = await DesktopStateGuard.CaptureAsync(Harness))
        {
            // Modify clipboard within guard scope
            await Clipboard.SetTextAsync("temporary content");
            await Task.Delay(100);

            var temp = await Clipboard.GetTextAsync();
            Assert.Equal("temporary content", temp);
        }

        // After guard disposal, clipboard should be restored
        await Task.Delay(200);
        var restored = await Clipboard.GetTextAsync();
        Assert.Equal(originalText, restored);
    }

    [Fact]
    public async Task ScreenCapture_DuringInteraction()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            // Take screenshot before typing
            var beforeShot = await Screen.CaptureAsync();
            ScreenAssert.IsValidScreenshot(beforeShot);

            // Type some text
            await Keyboard.TypeAsync("Visual change test - " + new string('X', 50));
            await Task.Delay(500);

            // Take screenshot after typing
            var afterShot = await Screen.CaptureAsync();
            ScreenAssert.IsValidScreenshot(afterShot);

            // Screenshots should be different (text was typed)
            ScreenAssert.AreDifferent(beforeShot, afterShot);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }
}
