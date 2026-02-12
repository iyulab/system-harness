using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class MouseTools(IHarness harness)
{
    [McpServerTool(Name = "mouse_click"), Description("Click at screen coordinates (x, y). Default is left button.")]
    public async Task<string> ClickAsync(
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        [Description("Mouse button: 'left' (default), 'right', or 'middle'.")] string button = "left",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var btn = ParseButton(button);
        await harness.Mouse.ClickAsync(x, y, btn, ct);
        ActionLog.Record("mouse_click", $"({x},{y}) {button}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Clicked ({x}, {y}) with {button} button.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_click_double"), Description("Double-click at screen coordinates (x, y).")]
    public async Task<string> DoubleClickAsync(
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await harness.Mouse.DoubleClickAsync(x, y, ct);
        ActionLog.Record("mouse_click_double", $"({x},{y})", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Double-clicked ({x}, {y}).", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_move"), Description("Move the mouse cursor to screen coordinates (x, y).")]
    public async Task<string> MoveAsync(
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await harness.Mouse.MoveAsync(x, y, ct);
        ActionLog.Record("mouse_move", $"({x},{y})", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Moved cursor to ({x}, {y}).", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_smooth_move"), Description("Smoothly move the mouse cursor to screen coordinates over a specified duration (animated movement).")]
    public async Task<string> SmoothMoveAsync(
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        [Description("Duration of the movement in milliseconds.")] int durationMs = 500,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (durationMs < 0)
            return McpResponse.Error("invalid_parameter", $"durationMs must be non-negative (got {durationMs}).", sw.ElapsedMilliseconds);
        await harness.Mouse.SmoothMoveAsync(x, y, TimeSpan.FromMilliseconds(durationMs), ct);
        ActionLog.Record("mouse_smooth_move", $"({x},{y}) duration={durationMs}ms", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Smoothly moved cursor to ({x}, {y}) over {durationMs}ms.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_drag"), Description("Drag from (fromX, fromY) to (toX, toY).")]
    public async Task<string> DragAsync(
        [Description("Start X coordinate.")] int fromX,
        [Description("Start Y coordinate.")] int fromY,
        [Description("End X coordinate.")] int toX,
        [Description("End Y coordinate.")] int toY,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await harness.Mouse.DragAsync(fromX, fromY, toX, toY, ct);
        ActionLog.Record("mouse_drag", $"({fromX},{fromY})→({toX},{toY})", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Dragged from ({fromX}, {fromY}) to ({toX}, {toY}).", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_scroll"), Description("Scroll at coordinates (x, y). Positive delta scrolls up, negative scrolls down.")]
    public async Task<string> ScrollAsync(
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        [Description("Scroll amount. Positive = up, negative = down. Typical: 3 for one notch.")] int delta,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await harness.Mouse.ScrollAsync(x, y, delta, ct);
        ActionLog.Record("mouse_scroll", $"({x},{y}) delta={delta}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Scrolled at ({x}, {y}) delta={delta}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_drag_window"), Description(
        "Drag within a window using window-relative coordinates. " +
        "Converts relative coordinates to absolute, then performs the drag.")]
    public async Task<string> DragWindowAsync(
        [Description("Window title (substring match, case-insensitive) or handle (integer as string).")] string titleOrHandle,
        [Description("Start X relative to window top-left.")] int fromRelX,
        [Description("Start Y relative to window top-left.")] int fromRelY,
        [Description("End X relative to window top-left.")] int toRelX,
        [Description("End Y relative to window top-left.")] int toRelY,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var windows = await harness.Window.ListAsync(ct);
        var win = ToolHelpers.FindWindow(windows, titleOrHandle);
        if (win is null)
            return McpResponse.Error("window_not_found", $"Window not found: '{titleOrHandle}'", sw.ElapsedMilliseconds);

        var absFromX = win.Bounds.X + fromRelX;
        var absFromY = win.Bounds.Y + fromRelY;
        var absToX = win.Bounds.X + toRelX;
        var absToY = win.Bounds.Y + toRelY;

        await harness.Mouse.DragAsync(absFromX, absFromY, absToX, absToY, ct);
        ActionLog.Record("mouse_drag_window", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);

        return McpResponse.Ok(new
        {
            from = new { x = absFromX, y = absFromY },
            to = new { x = absToX, y = absToY },
            window = new { handle = win.Handle.ToString(), win.Title },
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_scroll_horizontal"), Description("Horizontal scroll at coordinates (x, y). Positive delta scrolls right, negative scrolls left.")]
    public async Task<string> ScrollHorizontalAsync(
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        [Description("Scroll amount. Positive = right, negative = left. Typical: 3 for one notch.")] int delta,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await harness.Mouse.ScrollHorizontalAsync(x, y, delta, ct);
        ActionLog.Record("mouse_scroll_horizontal", $"({x},{y}) delta={delta}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Horizontal scrolled at ({x}, {y}) delta={delta}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_button_down"), Description("Press and hold a mouse button at screen coordinates. Must be paired with mouse_button_up.")]
    public async Task<string> ButtonDownAsync(
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        [Description("Mouse button: 'left' (default), 'right', or 'middle'.")] string button = "left",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var btn = ParseButton(button);
        await harness.Mouse.ButtonDownAsync(x, y, btn, ct);
        ActionLog.Record("mouse_button_down", $"({x},{y}) {button}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Button down ({x}, {y}) {button}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_button_up"), Description("Release a held mouse button at screen coordinates. Pair with mouse_button_down.")]
    public async Task<string> ButtonUpAsync(
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        [Description("Mouse button: 'left' (default), 'right', or 'middle'.")] string button = "left",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var btn = ParseButton(button);
        await harness.Mouse.ButtonUpAsync(x, y, btn, ct);
        ActionLog.Record("mouse_button_up", $"({x},{y}) {button}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Button up ({x}, {y}) {button}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "mouse_get"), Description("Get the current mouse cursor position.")]
    public async Task<string> GetPositionAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var (px, py) = await harness.Mouse.GetPositionAsync(ct);
        return McpResponse.Ok(new { x = px, y = py }, sw.ElapsedMilliseconds);
    }

    private static MouseButton ParseButton(string button) =>
        button.ToLowerInvariant() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left
        };
}
