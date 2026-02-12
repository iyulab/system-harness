using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class CoordTools(IHarness harness)
{
    [McpServerTool(Name = "coord_to_absolute"), Description(
        "Convert window-relative coordinates to absolute screen coordinates. " +
        "Given a window and a point (relX, relY) within it, returns the absolute (x, y) on screen.")]
    public async Task<string> ToAbsoluteAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("X offset relative to window top-left.")] int relX,
        [Description("Y offset relative to window top-left.")] int relY,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var windows = await harness.Window.ListAsync(ct);
        var win = ToolHelpers.FindWindow(windows, titleOrHandle);
        if (win is null)
            return McpResponse.Error("window_not_found", $"Window not found: '{titleOrHandle}'", sw.ElapsedMilliseconds);

        var absX = win.Bounds.X + relX;
        var absY = win.Bounds.Y + relY;

        return McpResponse.Ok(new
        {
            x = absX,
            y = absY,
            window = new { handle = win.Handle.ToString(), win.Title },
            windowBounds = new { win.Bounds.X, win.Bounds.Y, win.Bounds.Width, win.Bounds.Height },
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "coord_to_relative"), Description(
        "Convert absolute screen coordinates to window-relative coordinates. " +
        "Given a window and an absolute point (absX, absY) on screen, returns the relative (x, y) within the window.")]
    public async Task<string> ToRelativeAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Absolute screen X coordinate.")] int absX,
        [Description("Absolute screen Y coordinate.")] int absY,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var windows = await harness.Window.ListAsync(ct);
        var win = ToolHelpers.FindWindow(windows, titleOrHandle);
        if (win is null)
            return McpResponse.Error("window_not_found", $"Window not found: '{titleOrHandle}'", sw.ElapsedMilliseconds);

        var relX = absX - win.Bounds.X;
        var relY = absY - win.Bounds.Y;
        var inside = relX >= 0 && relY >= 0 && relX < win.Bounds.Width && relY < win.Bounds.Height;

        return McpResponse.Ok(new
        {
            x = relX,
            y = relY,
            inside,
            window = new { handle = win.Handle.ToString(), win.Title },
            windowBounds = new { win.Bounds.X, win.Bounds.Y, win.Bounds.Width, win.Bounds.Height },
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "coord_to_physical"), Description(
        "Convert logical (DPI-independent) coordinates to physical (DPI-dependent) coordinates. " +
        "Uses the monitor's scale factor at the given point.")]
    public async Task<string> ToPhysicalAsync(
        [Description("Logical (DPI-independent) X coordinate.")] int logicalX,
        [Description("Logical (DPI-independent) Y coordinate.")] int logicalY,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var monitor = await harness.Display.GetMonitorAtPointAsync(logicalX, logicalY, ct);
        var physX = (int)(logicalX * monitor.ScaleFactor);
        var physY = (int)(logicalY * monitor.ScaleFactor);

        return McpResponse.Ok(new
        {
            physicalX = physX,
            physicalY = physY,
            logicalX,
            logicalY,
            scaleFactor = monitor.ScaleFactor,
            monitor = new { monitor.Index, monitor.Name },
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "coord_scale_info"), Description(
        "Get DPI and scale factor information for a window's monitor. " +
        "Returns DPI values and scale factor useful for coordinate calculations.")]
    public async Task<string> ScaleInfoAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var monitor = await harness.Display.GetMonitorForWindowAsync(titleOrHandle, ct);

        return McpResponse.Ok(new
        {
            dpiX = monitor.DpiX,
            dpiY = monitor.DpiY,
            scaleFactor = monitor.ScaleFactor,
            monitor = new
            {
                monitor.Index, monitor.Name, monitor.IsPrimary,
                bounds = new { monitor.Bounds.X, monitor.Bounds.Y, monitor.Bounds.Width, monitor.Bounds.Height },
            },
        }, sw.ElapsedMilliseconds);
    }

}
