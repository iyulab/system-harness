using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class DisplayTools(IHarness harness)
{
    [McpServerTool(Name = "display_list"), Description("List all connected monitors with resolution, DPI, and bounds.")]
    public async Task<string> ListMonitorsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var monitors = await harness.Display.GetMonitorsAsync(ct);
        return McpResponse.Items(monitors.Select(m => new
        {
            m.Index, m.Name, m.IsPrimary,
            bounds = new { m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height },
            m.DpiX, m.DpiY, m.ScaleFactor,
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "display_get_primary"), Description("Get information about the primary monitor.")]
    public async Task<string> GetPrimaryAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var m = await harness.Display.GetPrimaryMonitorAsync(ct);
        return McpResponse.Ok(new
        {
            m.Index, m.Name,
            bounds = new { m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height },
            m.DpiX, m.DpiY, m.ScaleFactor,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "display_get_at_point"), Description("Get the monitor that contains the specified screen coordinates.")]
    public async Task<string> GetAtPointAsync(
        [Description("Screen X coordinate.")] int x,
        [Description("Screen Y coordinate.")] int y,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var m = await harness.Display.GetMonitorAtPointAsync(x, y, ct);
        return McpResponse.Ok(new
        {
            m.Index, m.Name, m.IsPrimary,
            bounds = new { m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height },
            m.DpiX, m.DpiY, m.ScaleFactor,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "display_get_for_window"), Description("Get the monitor that contains the majority of a window.")]
    public async Task<string> GetForWindowAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var m = await harness.Display.GetMonitorForWindowAsync(titleOrHandle, ct);
        return McpResponse.Ok(new
        {
            m.Index, m.Name, m.IsPrimary,
            bounds = new { m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height },
            m.DpiX, m.DpiY, m.ScaleFactor,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "display_get_virtual_bounds"), Description("Get the bounding rectangle of the virtual screen (all monitors combined).")]
    public async Task<string> GetVirtualBoundsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var r = await harness.Display.GetVirtualScreenBoundsAsync(ct);
        return McpResponse.Ok(new { r.X, r.Y, r.Width, r.Height }, sw.ElapsedMilliseconds);
    }
}
