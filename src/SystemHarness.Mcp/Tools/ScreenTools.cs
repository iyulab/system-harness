using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class ScreenTools(IHarness harness)
{
    [McpServerTool(Name = "screen_capture"), Description("Capture a full-screen screenshot. Saves to temp file and returns metadata (path, dimensions, size).")]
    public async Task<string> CaptureAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        using var screenshot = await harness.Screen.CaptureAsync(ct: ct);
        return await SaveAndDescribe(screenshot, "screen", sw, ct);
    }

    [McpServerTool(Name = "screen_capture_region"), Description("Capture a rectangular region of the screen. Saves to temp file and returns metadata.")]
    public async Task<string> CaptureRegionAsync(
        [Description("Top-left X coordinate of the region.")] int x,
        [Description("Top-left Y coordinate of the region.")] int y,
        [Description("Region width in pixels.")] int width,
        [Description("Region height in pixels.")] int height,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (width <= 0 || height <= 0)
            return McpResponse.Error("invalid_dimensions", $"Width and height must be positive (got {width}x{height}).", sw.ElapsedMilliseconds);
        using var screenshot = await harness.Screen.CaptureRegionAsync(x, y, width, height, ct);
        return await SaveAndDescribe(screenshot, "region", sw, ct);
    }

    [McpServerTool(Name = "screen_capture_window"), Description("Capture a specific window by title or handle. Saves to temp file and returns metadata.")]
    public async Task<string> CaptureWindowAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        using var screenshot = await harness.Screen.CaptureWindowAsync(titleOrHandle, ct);
        return await SaveAndDescribe(screenshot, "window", sw, ct);
    }

    [McpServerTool(Name = "screen_capture_monitor"), Description("Capture a specific monitor by zero-based index. Saves to temp file and returns metadata.")]
    public async Task<string> CaptureMonitorAsync(
        [Description("Zero-based monitor index (use display_list to see available monitors).")] int monitorIndex,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (monitorIndex < 0)
            return McpResponse.Error("invalid_parameter", $"monitorIndex must be non-negative (got {monitorIndex}).", sw.ElapsedMilliseconds);
        using var screenshot = await harness.Screen.CaptureMonitorAsync(monitorIndex, ct: ct);
        return await SaveAndDescribe(screenshot, $"monitor{monitorIndex}", sw, ct);
    }

    [McpServerTool(Name = "screen_capture_window_region"), Description("Capture a rectangular region within a window using window-relative coordinates.")]
    public async Task<string> CaptureWindowRegionAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Relative X coordinate within the window.")] int relativeX,
        [Description("Relative Y coordinate within the window.")] int relativeY,
        [Description("Region width in pixels.")] int width,
        [Description("Region height in pixels.")] int height,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (width <= 0 || height <= 0)
            return McpResponse.Error("invalid_dimensions", $"Width and height must be positive (got {width}x{height}).", sw.ElapsedMilliseconds);
        using var screenshot = await harness.Screen.CaptureWindowRegionAsync(titleOrHandle, relativeX, relativeY, width, height, ct: ct);
        return await SaveAndDescribe(screenshot, "winregion", sw, ct);
    }

    private static async Task<string> SaveAndDescribe(Screenshot screenshot, string prefix, Stopwatch sw, CancellationToken ct)
    {
        var ext = screenshot.MimeType == "image/png" ? "png" : "jpg";
        var path = Path.Combine(Path.GetTempPath(), $"harness-{prefix}-{DateTime.Now:HHmmss}.{ext}");
        await screenshot.SaveAsync(path, ct);

        return McpResponse.Ok(new
        {
            path,
            screenshot.Width,
            screenshot.Height,
            format = screenshot.MimeType,
            sizeBytes = screenshot.Bytes.Length,
            timestamp = screenshot.Timestamp.ToString("o"),
        }, sw.ElapsedMilliseconds);
    }
}
