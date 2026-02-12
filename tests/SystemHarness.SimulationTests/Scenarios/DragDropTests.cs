namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests drag and drop operations.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Local")]
public class DragDropTests : SimulationTestBase
{
    public DragDropTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CoordinateBasedDrag_MovesFromAToB()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            // Type some text
            await Keyboard.TypeAsync("Select this text for drag test");
            await Task.Delay(300);

            // Get window bounds for coordinate calculation
            var wins = await Window.FindByProcessIdAsync(proc.Pid);
            Assert.NotEmpty(wins);

            var bounds = wins[0].Bounds;

            // Perform a drag operation within the window
            // (Start from center-left to center-right of text area)
            var startX = bounds.X + 50;
            var startY = bounds.Y + 60;
            var endX = bounds.X + 300;
            var endY = bounds.Y + 60;

            await Mouse.DragAsync(startX, startY, endX, endY);
            await Task.Delay(300);

            // Drag completed without throwing
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task ButtonDownUp_ManualDrag()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            await Keyboard.TypeAsync("Manual drag test text");
            await Task.Delay(300);

            var wins = await Window.FindByProcessIdAsync(proc.Pid);
            var bounds = wins[0].Bounds;

            // Manual drag using ButtonDown/Move/ButtonUp
            var startX = bounds.X + 50;
            var y = bounds.Y + 60;

            await Mouse.ButtonDownAsync(startX, y);
            await Task.Delay(50);

            // Move in steps
            for (var x = startX; x <= startX + 200; x += 20)
            {
                await Mouse.MoveAsync(x, y);
                await Task.Delay(20);
            }

            await Mouse.ButtonUpAsync(startX + 200, y);
            await Task.Delay(300);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }
}
