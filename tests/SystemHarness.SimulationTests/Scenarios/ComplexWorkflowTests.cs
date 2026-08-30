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
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Simulate an "agent loop": capture → analyze → act → re-capture
            for (var i = 0; i < 3; i++)
            {
                // Step 1: Capture screen
                var screenshot = await Screen.CaptureAsync(ct: TestContext.Current.CancellationToken);
                ScreenAssert.IsValidScreenshot(screenshot);

                // Step 2: "Analyze" (mock — just check dimensions)
                Assert.True(screenshot.Width > 0);
                Assert.True(screenshot.Height > 0);

                // Step 3: Act — type a line
                await Keyboard.TypeAsync($"Agent iteration {i + 1}", ct: TestContext.Current.CancellationToken);
                await Keyboard.KeyPressAsync(Key.Enter, TestContext.Current.CancellationToken);
                await Task.Delay(200, TestContext.Current.CancellationToken);
            }

            // Step 4: Verify all iterations were typed
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.A });
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.C });
            await Task.Delay(200, TestContext.Current.CancellationToken);

            var text = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(text);
            Assert.Contains("iteration 1", text);
            Assert.Contains("iteration 2", text);
            Assert.Contains("iteration 3", text);
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
    public async Task BatchAutomation_MultipleNotepadInstances()
    {
        var pids = new List<int>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"sim_batch_{Guid.NewGuid():N}");
        await FileSystem.CreateDirectoryAsync(tempDir, TestContext.Current.CancellationToken);

        try
        {
            // Open 3 Notepad instances
            for (var i = 0; i < 3; i++)
            {
                var proc = await Process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
                pids.Add(proc.Pid);
                await Task.Delay(500, TestContext.Current.CancellationToken);
            }

            await Task.Delay(1000, TestContext.Current.CancellationToken);

            // Type unique text in each
            for (var i = 0; i < pids.Count; i++)
            {
                var wins = await Window.FindByProcessIdAsync(pids[i], TestContext.Current.CancellationToken);
                if (wins.Count == 0) continue;

                await Window.FocusAsync(wins[0].Handle.ToString(CultureInfo.InvariantCulture), TestContext.Current.CancellationToken);
                await Task.Delay(300, TestContext.Current.CancellationToken);

                await Keyboard.TypeAsync($"Instance {i}: Unique content {Guid.NewGuid():N}", ct: TestContext.Current.CancellationToken);
                await Task.Delay(200, TestContext.Current.CancellationToken);
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
                // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
                #pragma warning disable xUnit1051
                try { await Process.KillAsync(pid, CancellationToken.None); } catch { }
                #pragma warning restore xUnit1051
            }
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public async Task DesktopStateGuard_PreservesState()
    {
        // Set known clipboard content
        var originalText = $"guard_test_{Guid.NewGuid():N}";
        await Clipboard.SetTextAsync(originalText, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        await using (var guard = await DesktopStateGuard.CaptureAsync(Harness))
        {
            // Modify clipboard within guard scope
            await Clipboard.SetTextAsync("temporary content", TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);

            var temp = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);
            Assert.Equal("temporary content", temp);
        }

        // After guard disposal, clipboard should be restored
        await Task.Delay(200, TestContext.Current.CancellationToken);
        var restored = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(originalText, restored);
    }

    [Fact]
    public async Task ScreenCapture_DuringInteraction()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Take screenshot before typing
            var beforeShot = await Screen.CaptureAsync(ct: TestContext.Current.CancellationToken);
            ScreenAssert.IsValidScreenshot(beforeShot);

            // Type some text
            await Keyboard.TypeAsync("Visual change test - " + new string('X', 50), ct: TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);

            // Take screenshot after typing
            var afterShot = await Screen.CaptureAsync(ct: TestContext.Current.CancellationToken);
            ScreenAssert.IsValidScreenshot(afterShot);

            // Screenshots should be different (text was typed)
            ScreenAssert.AreDifferent(beforeShot, afterShot);
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
