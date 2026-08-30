namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests drag and drop operations.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Integration")]
public class DragDropTests : SimulationTestBase
{
    public DragDropTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CoordinateBasedDrag_MovesFromAToB()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Type some text
            await Keyboard.TypeAsync("Select this text for drag test", ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Get window bounds for coordinate calculation
            var wins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            Assert.NotEmpty(wins);

            var bounds = wins[0].Bounds;

            // Perform a drag operation within the window
            // (Start from center-left to center-right of text area)
            var startX = bounds.X + 50;
            var startY = bounds.Y + 60;
            var endX = bounds.X + 300;
            var endY = bounds.Y + 60;

            await Mouse.DragAsync(startX, startY, endX, endY, TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Drag completed without throwing
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
    public async Task ButtonDownUp_ManualDrag()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            await Keyboard.TypeAsync("Manual drag test text", ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            var wins = await Window.FindByProcessIdAsync(proc.Pid, TestContext.Current.CancellationToken);
            var bounds = wins[0].Bounds;

            // Manual drag using ButtonDown/Move/ButtonUp
            var startX = bounds.X + 50;
            var y = bounds.Y + 60;

            await Mouse.ButtonDownAsync(startX, y, ct: TestContext.Current.CancellationToken);
            await Task.Delay(50, TestContext.Current.CancellationToken);

            // Move in steps
            for (var x = startX; x <= startX + 200; x += 20)
            {
                await Mouse.MoveAsync(x, y, TestContext.Current.CancellationToken);
                await Task.Delay(20, TestContext.Current.CancellationToken);
            }

            await Mouse.ButtonUpAsync(startX + 200, y, ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);
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
