using SystemHarness.Windows;

namespace SystemHarness.Tests.Input;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class KeyboardExtensionTests
{
    private readonly WindowsKeyboard _keyboard = new();

    [Fact]
    public async Task IsKeyPressedAsync_ReturnsFalseForUnpressedKey()
    {
        var isPressed = await _keyboard.IsKeyPressedAsync(Key.F12);

        // F12 should not be pressed during tests
        Assert.False(isPressed);
    }

    [Fact]
    public async Task IsKeyPressedAsync_DoesNotThrowForModifierKeys()
    {
        // These should not throw regardless of state
        await _keyboard.IsKeyPressedAsync(Key.Ctrl);
        await _keyboard.IsKeyPressedAsync(Key.Alt);
        await _keyboard.IsKeyPressedAsync(Key.Shift);
    }

    [Fact]
    public async Task ToggleKeyAsync_ThrowsForNonToggleKey()
    {
        await Assert.ThrowsAsync<HarnessException>(async () =>
        {
            await _keyboard.ToggleKeyAsync(Key.A, true);
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
            await _keyboard.ToggleKeyAsync(Key.CapsLock, !initialState);
            await Task.Delay(100);
        }
        finally
        {
            // Restore original state
            await _keyboard.ToggleKeyAsync(Key.CapsLock, initialState);
        }
    }
}
