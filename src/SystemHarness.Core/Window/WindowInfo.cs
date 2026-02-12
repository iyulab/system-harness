namespace SystemHarness;

/// <summary>
/// Information about a desktop window.
/// </summary>
public sealed class WindowInfo
{
    /// <summary>
    /// Platform-specific window handle (HWND on Windows).
    /// </summary>
    public required nint Handle { get; init; }

    /// <summary>
    /// Window title text.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// PID of the process that owns this window.
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// Whether the window is currently visible.
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Window bounding rectangle (screen coordinates).
    /// </summary>
    public Rectangle Bounds { get; init; }

    /// <summary>
    /// Window class name (e.g., "Notepad", "Chrome_WidgetWin_1").
    /// </summary>
    public string? ClassName { get; init; }

    /// <summary>
    /// Handle of the parent window, if any.
    /// </summary>
    public nint? ParentHandle { get; init; }

    /// <summary>
    /// Current window state (Normal, Minimized, Maximized).
    /// </summary>
    public WindowState State { get; init; } = WindowState.Normal;
}
