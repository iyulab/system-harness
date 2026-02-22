using SystemHarness.Windows;

namespace SystemHarness.Tests.Input;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
[Trait("Category", "RequiresDesktop")]
public class MouseExtensionTests
{
    private readonly WindowsMouse _mouse = new();

    [Fact]
    public async Task MiddleClickAsync_DoesNotThrow()
    {
        var (x, y) = await _mouse.GetPositionAsync();
        await _mouse.MiddleClickAsync(x, y);
    }

    [Fact]
    public async Task ScrollHorizontalAsync_DoesNotThrow()
    {
        var (x, y) = await _mouse.GetPositionAsync();
        await _mouse.ScrollHorizontalAsync(x, y, 1);
        await _mouse.ScrollHorizontalAsync(x, y, -1);
    }

    [Fact]
    public async Task ButtonDownUp_WorksCorrectly()
    {
        var (x, y) = await _mouse.GetPositionAsync();

        // Press and release should not throw
        await _mouse.ButtonDownAsync(x, y, MouseButton.Left);
        await Task.Delay(50);
        await _mouse.ButtonUpAsync(x, y, MouseButton.Left);
    }

    [Fact]
    public async Task SmoothMoveAsync_MovesToTarget()
    {
        var startPos = await _mouse.GetPositionAsync();
        var targetX = startPos.X + 50;
        var targetY = startPos.Y + 50;

        await _mouse.SmoothMoveAsync(targetX, targetY, TimeSpan.FromMilliseconds(200));

        var endPos = await _mouse.GetPositionAsync();
        // Allow ±2 pixel tolerance
        Assert.InRange(endPos.X, targetX - 2, targetX + 2);
        Assert.InRange(endPos.Y, targetY - 2, targetY + 2);
    }

    [Fact]
    public async Task SmoothMoveAsync_CancellationWorks()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _mouse.SmoothMoveAsync(9999, 9999, TimeSpan.FromSeconds(10), cts.Token);
        });
    }
}
