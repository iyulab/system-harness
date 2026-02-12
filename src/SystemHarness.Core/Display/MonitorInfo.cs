namespace SystemHarness;

/// <summary>
/// Information about a display monitor.
/// </summary>
public sealed class MonitorInfo
{
    /// <summary>
    /// Zero-based monitor index.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Display device name (e.g., "\\.\DISPLAY1").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full monitor bounds in virtual screen coordinates.
    /// </summary>
    public required Rectangle Bounds { get; init; }

    /// <summary>
    /// Usable work area (excludes taskbar and docked windows).
    /// </summary>
    public Rectangle WorkArea { get; init; }

    /// <summary>
    /// Whether this is the primary display.
    /// </summary>
    public bool IsPrimary { get; init; }

    /// <summary>
    /// Horizontal DPI (dots per inch).
    /// </summary>
    public double DpiX { get; init; }

    /// <summary>
    /// Vertical DPI (dots per inch).
    /// </summary>
    public double DpiY { get; init; }

    /// <summary>
    /// Display scale factor (e.g., 1.0, 1.25, 1.5, 2.0).
    /// </summary>
    public double ScaleFactor { get; init; }

    /// <summary>
    /// Platform-specific monitor handle (HMONITOR on Windows).
    /// </summary>
    public nint Handle { get; init; }
}
