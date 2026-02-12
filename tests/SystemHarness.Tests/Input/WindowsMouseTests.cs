using SystemHarness.Windows;

namespace SystemHarness.Tests.Input;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class WindowsMouseTests
{
    private readonly WindowsMouse _mouse = new();

    [Fact]
    public async Task GetPositionAsync_ReturnsValidCoordinates()
    {
        var (x, y) = await _mouse.GetPositionAsync();

        // Cursor should be within reasonable screen bounds
        Assert.True(x >= 0);
        Assert.True(y >= 0);
    }

    [Fact]
    public async Task MoveAsync_ChangesCursorPosition()
    {
        // Save original position
        var (origX, origY) = await _mouse.GetPositionAsync();

        try
        {
            await _mouse.MoveAsync(500, 500);
            await Task.Delay(100);

            var (newX, newY) = await _mouse.GetPositionAsync();

            // Allow some tolerance for DPI scaling
            Assert.InRange(newX, 490, 510);
            Assert.InRange(newY, 490, 510);
        }
        finally
        {
            // Restore original position
            await _mouse.MoveAsync(origX, origY);
        }
    }

    [Fact]
    public async Task ClickAsync_DoesNotThrow()
    {
        // Save original position
        var (origX, origY) = await _mouse.GetPositionAsync();

        try
        {
            // Click in a safe area (center of screen)
            await _mouse.ClickAsync(500, 500);
        }
        finally
        {
            await _mouse.MoveAsync(origX, origY);
        }
    }

    [Fact]
    public async Task ScrollAsync_DoesNotThrow()
    {
        var (origX, origY) = await _mouse.GetPositionAsync();
        try
        {
            await _mouse.ScrollAsync(500, 500, 3); // scroll up
            await _mouse.ScrollAsync(500, 500, -3); // scroll down
        }
        finally
        {
            await _mouse.MoveAsync(origX, origY);
        }
    }

    [Fact]
    public async Task DoubleClickAsync_DoesNotThrow()
    {
        var (origX, origY) = await _mouse.GetPositionAsync();
        try
        {
            await _mouse.DoubleClickAsync(500, 500);
        }
        finally
        {
            await _mouse.MoveAsync(origX, origY);
        }
    }

    [Fact]
    public async Task RightClickAsync_DoesNotThrow()
    {
        var (origX, origY) = await _mouse.GetPositionAsync();
        try
        {
            await _mouse.RightClickAsync(500, 500);
            await Task.Delay(100);
            // Press Escape to close any context menu
            await _mouse.ClickAsync(500, 500);
        }
        finally
        {
            await _mouse.MoveAsync(origX, origY);
        }
    }

    [Fact]
    public async Task DragAsync_MovesCursorFromToPosition()
    {
        var (origX, origY) = await _mouse.GetPositionAsync();
        try
        {
            await _mouse.DragAsync(300, 300, 600, 600);
            await Task.Delay(100);

            var (endX, endY) = await _mouse.GetPositionAsync();
            Assert.InRange(endX, 590, 610);
            Assert.InRange(endY, 590, 610);
        }
        finally
        {
            await _mouse.MoveAsync(origX, origY);
        }
    }
}
