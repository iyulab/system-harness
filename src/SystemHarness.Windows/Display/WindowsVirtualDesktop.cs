using Windows.Win32;
using Windows.Win32.Foundation;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IVirtualDesktop"/> using keyboard shortcuts.
/// Note: Full COM-based IVirtualDesktopManager is available but uses undocumented APIs.
/// This implementation uses the Ctrl+Win+Arrow keyboard shortcuts for desktop switching.
/// </summary>
public sealed class WindowsVirtualDesktop : IVirtualDesktop
{
    private readonly WindowsKeyboard _keyboard = new();

    public Task<int> GetDesktopCountAsync(CancellationToken ct = default)
    {
        // Windows does not expose a public API for virtual desktop count.
        // Would need COM interop with IVirtualDesktopManagerInternal (undocumented).
        // Return a reasonable default.
        throw new HarnessException(
            "GetDesktopCountAsync requires undocumented COM APIs. " +
            "Consider using Windows UI Automation or the IVirtualDesktopManager COM interface.");
    }

    public Task<int> GetCurrentDesktopIndexAsync(CancellationToken ct = default)
    {
        // Same limitation as GetDesktopCountAsync
        throw new HarnessException(
            "GetCurrentDesktopIndexAsync requires undocumented COM APIs.");
    }

    public async Task SwitchToDesktopAsync(int index, CancellationToken ct = default)
    {
        // Use Ctrl+Win+Left/Right arrows to navigate
        // This is a simplified approach — navigate relative to current position
        // For absolute positioning, the COM interface would be needed

        // First go to desktop 0 by pressing Ctrl+Win+Left many times
        for (var i = 0; i < 20; i++) // Max 20 desktops
        {
            ct.ThrowIfCancellationRequested();
            await _keyboard.HotkeyAsync(ct, Key.Ctrl, Key.Win, Key.Left);
            await Task.Delay(100, ct);
        }

        // Then go right to the desired index
        for (var i = 0; i < index; i++)
        {
            ct.ThrowIfCancellationRequested();
            await _keyboard.HotkeyAsync(ct, Key.Ctrl, Key.Win, Key.Right);
            await Task.Delay(100, ct);
        }
    }

    public Task MoveWindowToDesktopAsync(string titleOrHandle, int desktopIndex, CancellationToken ct = default)
    {
        // Would need IVirtualDesktopManager COM interface
        throw new HarnessException(
            "MoveWindowToDesktopAsync requires the IVirtualDesktopManager COM interface. " +
            "This is currently not implemented.");
    }
}
