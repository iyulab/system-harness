using SystemHarness.Windows;

namespace SystemHarness.Tests.Input;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
[Trait("Category", "RequiresDesktop")]
public class KeyboardExtensionTests
{
    private readonly WindowsKeyboard _keyboard = new();

    [Fact]
    public async Task IsKeyPressedAsync_ReturnsFalseForUnpressedKey()
    {
        var isPressed = await _keyboard.IsKeyPressedAsync(Key.F12, TestContext.Current.CancellationToken);

        // F12 should not be pressed during tests
        Assert.False(isPressed);
    }

    [Fact]
    public async Task IsKeyPressedAsync_DoesNotThrowForModifierKeys()
    {
        // These should not throw regardless of state
        await _keyboard.IsKeyPressedAsync(Key.Ctrl, TestContext.Current.CancellationToken);
        await _keyboard.IsKeyPressedAsync(Key.Alt, TestContext.Current.CancellationToken);
        await _keyboard.IsKeyPressedAsync(Key.Shift, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ToggleKeyAsync_ThrowsForNonToggleKey()
    {
        await Assert.ThrowsAsync<HarnessException>(async () =>
        {
            await _keyboard.ToggleKeyAsync(Key.A, true, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task ToggleKeyAsync_CapsLock_TogglesAndRestores()
    {
        // Get current CapsLock state
        var initialState = Console.CapsLock;

        try
        {
            // Toggle to opposite
            await _keyboard.ToggleKeyAsync(Key.CapsLock, !initialState, TestContext.Current.CancellationToken);
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        finally
        {
            // Restore original state
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this cleanup matters most.
            #pragma warning disable xUnit1051
            await _keyboard.ToggleKeyAsync(Key.CapsLock, initialState, CancellationToken.None);
            #pragma warning restore xUnit1051
        }
    }
}
