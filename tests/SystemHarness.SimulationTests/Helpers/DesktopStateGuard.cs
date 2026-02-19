using System.Globalization;

namespace SystemHarness.SimulationTests.Helpers;

/// <summary>
/// Saves and restores desktop state (clipboard, cursor position, foreground window)
/// to prevent simulation tests from leaving side effects.
/// </summary>
public sealed class DesktopStateGuard : IAsyncDisposable
{
    private readonly IClipboard _clipboard;
    private readonly IMouse _mouse;
    private readonly IWindow _window;

    private string? _savedClipboardText;
    private (int X, int Y) _savedCursorPos;
    private nint _savedForegroundHandle;

    private DesktopStateGuard(IClipboard clipboard, IMouse mouse, IWindow window)
    {
        _clipboard = clipboard;
        _mouse = mouse;
        _window = window;
    }

    /// <summary>
    /// Creates a guard that captures the current desktop state.
    /// Dispose the guard to restore the state.
    /// </summary>
    public static async Task<DesktopStateGuard> CaptureAsync(IHarness harness)
    {
        var guard = new DesktopStateGuard(harness.Clipboard, harness.Mouse, harness.Window);
        await guard.SaveStateAsync();
        return guard;
    }

    private async Task SaveStateAsync()
    {
        try { _savedClipboardText = await _clipboard.GetTextAsync(); } catch { }
        try { _savedCursorPos = await _mouse.GetPositionAsync(); } catch { }
        try
        {
            var fg = await _window.GetForegroundAsync();
            _savedForegroundHandle = fg?.Handle ?? 0;
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        // Restore clipboard
        try
        {
            if (_savedClipboardText is not null)
                await _clipboard.SetTextAsync(_savedClipboardText);
        }
        catch { }

        // Restore cursor position
        try
        {
            await _mouse.MoveAsync(_savedCursorPos.X, _savedCursorPos.Y);
        }
        catch { }

        // Restore foreground window
        try
        {
            if (_savedForegroundHandle != 0)
                await _window.FocusAsync(_savedForegroundHandle.ToString(CultureInfo.InvariantCulture));
        }
        catch { }
    }
}
