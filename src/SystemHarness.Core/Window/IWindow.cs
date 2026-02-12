namespace SystemHarness;

/// <summary>
/// Window management — enumerate, focus, resize, move, close, and advanced window control.
/// </summary>
public interface IWindow
{
    /// <summary>
    /// Lists all visible top-level windows.
    /// </summary>
    Task<IReadOnlyList<WindowInfo>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Brings the specified window to the foreground and gives it input focus.
    /// </summary>
    Task FocusAsync(string titleOrHandle, CancellationToken ct = default);

    /// <summary>
    /// Minimizes the specified window to the taskbar.
    /// </summary>
    Task MinimizeAsync(string titleOrHandle, CancellationToken ct = default);

    /// <summary>
    /// Maximizes the specified window to fill the screen.
    /// </summary>
    Task MaximizeAsync(string titleOrHandle, CancellationToken ct = default);

    /// <summary>
    /// Resizes the specified window to the given width and height.
    /// </summary>
    Task ResizeAsync(string titleOrHandle, int width, int height, CancellationToken ct = default);

    /// <summary>
    /// Moves the specified window to the given screen coordinates.
    /// </summary>
    Task MoveAsync(string titleOrHandle, int x, int y, CancellationToken ct = default);

    /// <summary>
    /// Closes the specified window.
    /// </summary>
    Task CloseAsync(string titleOrHandle, CancellationToken ct = default);

    // --- Phase 7 Extensions (DIM for backward compatibility) ---

    /// <summary>
    /// Restores a minimized or maximized window to its normal state.
    /// </summary>
    Task RestoreAsync(string titleOrHandle, CancellationToken ct = default)
        => throw new NotSupportedException("RestoreAsync is not supported by this implementation.");

    /// <summary>
    /// Hides a window (makes it invisible without closing).
    /// </summary>
    Task HideAsync(string titleOrHandle, CancellationToken ct = default)
        => throw new NotSupportedException("HideAsync is not supported by this implementation.");

    /// <summary>
    /// Shows a previously hidden window.
    /// </summary>
    Task ShowAsync(string titleOrHandle, CancellationToken ct = default)
        => throw new NotSupportedException("ShowAsync is not supported by this implementation.");

    /// <summary>
    /// Toggles the always-on-top (topmost) state of a window.
    /// </summary>
    Task SetAlwaysOnTopAsync(string titleOrHandle, bool onTop, CancellationToken ct = default)
        => throw new NotSupportedException("SetAlwaysOnTopAsync is not supported by this implementation.");

    /// <summary>
    /// Sets the window opacity (transparency). 0.0 = fully transparent, 1.0 = fully opaque.
    /// </summary>
    Task SetOpacityAsync(string titleOrHandle, double opacity, CancellationToken ct = default)
        => throw new NotSupportedException("SetOpacityAsync is not supported by this implementation.");

    /// <summary>
    /// Gets information about the current foreground (focused) window.
    /// </summary>
    Task<WindowInfo?> GetForegroundAsync(CancellationToken ct = default)
        => throw new NotSupportedException("GetForegroundAsync is not supported by this implementation.");

    /// <summary>
    /// Gets the current state (Normal, Minimized, Maximized) of a window.
    /// </summary>
    Task<WindowState> GetStateAsync(string titleOrHandle, CancellationToken ct = default)
        => throw new NotSupportedException("GetStateAsync is not supported by this implementation.");

    /// <summary>
    /// Waits for a window with the given title substring to appear.
    /// Returns the window info when found, or throws on timeout.
    /// </summary>
    Task<WindowInfo> WaitForWindowAsync(string titleSubstring, TimeSpan? timeout = null, CancellationToken ct = default)
        => throw new NotSupportedException("WaitForWindowAsync is not supported by this implementation.");

    /// <summary>
    /// Finds all windows belonging to a specific process.
    /// </summary>
    Task<IReadOnlyList<WindowInfo>> FindByProcessIdAsync(int pid, CancellationToken ct = default)
        => throw new NotSupportedException("FindByProcessIdAsync is not supported by this implementation.");

    /// <summary>
    /// Gets all child windows of a specified parent window.
    /// </summary>
    Task<IReadOnlyList<WindowInfo>> GetChildWindowsAsync(string titleOrHandle, CancellationToken ct = default)
        => throw new NotSupportedException("GetChildWindowsAsync is not supported by this implementation.");
}
