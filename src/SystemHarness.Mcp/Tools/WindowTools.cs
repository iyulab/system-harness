using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SystemHarness.Mcp.Tools;

public sealed class WindowTools(IHarness harness)
{
    [McpServerTool(Name = "window_list"), Description("List all visible windows with titles, handles, bounds, and process IDs.")]
    public async Task<string> ListAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var windows = await harness.Window.ListAsync(ct);
        return McpResponse.Items(windows.Select(w => new
        {
            handle = (long)w.Handle, w.Title, w.ProcessId,
            bounds = new { w.Bounds.X, w.Bounds.Y, w.Bounds.Width, w.Bounds.Height },
            state = w.State.ToString(),
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_focus"), Description("Bring a window to the foreground by title substring or handle.")]
    public async Task<string> FocusAsync(
        [Description("Window title (substring match, case-insensitive) or handle (integer as string).")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.FocusAsync(titleOrHandle, ct);
        ActionLog.Record("window_focus", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Focused window: {titleOrHandle}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_resize"), Description("Resize a window to the specified dimensions.")]
    public async Task<string> ResizeAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("New window width in pixels.")] int width,
        [Description("New window height in pixels.")] int height,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (width <= 0 || height <= 0)
            return McpResponse.Error("invalid_dimensions", $"Width and height must be positive (got {width}x{height}).", sw.ElapsedMilliseconds);
        await harness.Window.ResizeAsync(titleOrHandle, width, height, ct);
        ActionLog.Record("window_resize", $"window={titleOrHandle}, {width}x{height}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Resized '{titleOrHandle}' to {width}x{height}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_close"), Description("Close a window by title substring or handle.")]
    public async Task<string> CloseAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.CloseAsync(titleOrHandle, ct);
        ActionLog.Record("window_close", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Closed window: {titleOrHandle}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_minimize"), Description("Minimize a window.")]
    public async Task<string> MinimizeAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.MinimizeAsync(titleOrHandle, ct);
        ActionLog.Record("window_minimize", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Minimized: {titleOrHandle}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_maximize"), Description("Maximize a window.")]
    public async Task<string> MaximizeAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.MaximizeAsync(titleOrHandle, ct);
        ActionLog.Record("window_maximize", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Maximized: {titleOrHandle}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_restore"), Description("Restore a window to its normal size (undo minimize or maximize).")]
    public async Task<string> RestoreAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.RestoreAsync(titleOrHandle, ct);
        ActionLog.Record("window_restore", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Restored: {titleOrHandle}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_move"), Description("Move a window to screen coordinates (x, y).")]
    public async Task<string> MoveAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Target screen X coordinate.")] int x,
        [Description("Target screen Y coordinate.")] int y,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.MoveAsync(titleOrHandle, x, y, ct);
        ActionLog.Record("window_move", $"window={titleOrHandle}, ({x},{y})", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Moved '{titleOrHandle}' to ({x}, {y}).", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_hide"), Description("Hide a window (make it invisible but keep the process running).")]
    public async Task<string> HideAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.HideAsync(titleOrHandle, ct);
        ActionLog.Record("window_hide", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Hidden: {titleOrHandle}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_show"), Description("Show a previously hidden window (make it visible again).")]
    public async Task<string> ShowAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.ShowAsync(titleOrHandle, ct);
        ActionLog.Record("window_show", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Shown: {titleOrHandle}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_set_always_on_top"), Description("Set or clear the always-on-top (topmost) flag for a window.")]
    public async Task<string> SetAlwaysOnTopAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("True to make the window always on top, false to clear.")] bool onTop,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Window.SetAlwaysOnTopAsync(titleOrHandle, onTop, ct);
        ActionLog.Record("window_set_always_on_top", $"window={titleOrHandle}, onTop={onTop}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"{(onTop ? "Set" : "Cleared")} always-on-top for: {titleOrHandle}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_set_opacity"), Description("Set the opacity (transparency) of a window. 0.0 = fully transparent, 1.0 = fully opaque.")]
    public async Task<string> SetOpacityAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Opacity value from 0.0 (transparent) to 1.0 (opaque).")] double opacity,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (opacity < 0.0 || opacity > 1.0)
            return McpResponse.Error("invalid_parameter", $"opacity must be 0.0-1.0 (got {opacity}).", sw.ElapsedMilliseconds);
        await harness.Window.SetOpacityAsync(titleOrHandle, opacity, ct);
        ActionLog.Record("window_set_opacity", $"window={titleOrHandle}, opacity={opacity:F2}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Set opacity of '{titleOrHandle}' to {opacity:F2}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_get_children"), Description("Get child windows of a window (popups, dialogs, owned windows).")]
    public async Task<string> GetChildrenAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var children = await harness.Window.GetChildWindowsAsync(titleOrHandle, ct);
        return McpResponse.Items(children.Select(w => new
        {
            handle = (long)w.Handle, w.Title, w.ProcessId,
            bounds = new { w.Bounds.X, w.Bounds.Y, w.Bounds.Width, w.Bounds.Height },
            state = w.State.ToString(),
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_find_by_pid"), Description("Find all windows belonging to a specific process by PID.")]
    public async Task<string> FindByPidAsync(
        [Description("Process ID to find windows for.")] int pid,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var windows = await harness.Window.FindByProcessIdAsync(pid, ct);
        return McpResponse.Items(windows.Select(w => new
        {
            handle = (long)w.Handle, w.Title, w.ProcessId,
            bounds = new { w.Bounds.X, w.Bounds.Y, w.Bounds.Width, w.Bounds.Height },
            state = w.State.ToString(),
        }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_get"), Description("Get detailed info about a specific window by title or handle.")]
    public async Task<string> GetAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var windows = await harness.Window.ListAsync(ct);
        var win = ToolHelpers.FindWindow(windows, titleOrHandle);
        if (win is null)
            return McpResponse.Error("window_not_found", $"Window not found: '{titleOrHandle}'", sw.ElapsedMilliseconds);
        var state = await harness.Window.GetStateAsync(titleOrHandle, ct);

        return McpResponse.Ok(new
        {
            handle = win.Handle.ToString(),
            win.Title,
            win.ProcessId,
            win.ClassName,
            state = state.ToString(),
            bounds = new { win.Bounds.X, win.Bounds.Y, win.Bounds.Width, win.Bounds.Height },
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_get_foreground"), Description("Get the currently focused foreground window.")]
    public async Task<string> GetForegroundAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var win = await harness.Window.GetForegroundAsync(ct);
        if (win is null)
            return McpResponse.Ok(new { found = false }, sw.ElapsedMilliseconds);

        return McpResponse.Ok(new
        {
            found = true,
            handle = win.Handle.ToString(),
            win.Title,
            win.ProcessId,
            bounds = new { win.Bounds.X, win.Bounds.Y, win.Bounds.Width, win.Bounds.Height },
            state = win.State.ToString(),
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_wait"), Description(
        "Wait for a window with the given title to appear. " +
        "Polls until a matching window is found or timeout is reached.")]
    public async Task<string> WaitAsync(
        [Description("Window title substring to wait for (case-insensitive).")] string title,
        [Description("Maximum time to wait in milliseconds.")] int timeoutMs = 10000,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(title))
            return McpResponse.Error("invalid_parameter", "title cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);
        try
        {
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            var win = await harness.Window.WaitForWindowAsync(title, timeout, ct);
            return McpResponse.Ok(new
            {
                found = true,
                handle = win.Handle.ToString(),
                win.Title,
                win.ProcessId,
                bounds = new { win.Bounds.X, win.Bounds.Y, win.Bounds.Width, win.Bounds.Height },
            }, sw.ElapsedMilliseconds);
        }
        catch (TimeoutException)
        {
            return McpResponse.Ok(new
            {
                found = false,
                title,
            }, sw.ElapsedMilliseconds);
        }
    }

    [McpServerTool(Name = "window_wait_close"), Description(
        "Wait for a window to close (disappear). " +
        "Polls until the window is no longer visible or timeout is reached.")]
    public async Task<string> WaitCloseAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Maximum time to wait in milliseconds.")] int timeoutMs = 10000,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);
        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(500);

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var windows = await harness.Window.ListAsync(ct);
            var win = ToolHelpers.FindWindow(windows, titleOrHandle);
            if (win is null)
            {
                return McpResponse.Ok(new
                {
                    closed = true,
                    titleOrHandle,
                }, sw.ElapsedMilliseconds);
            }

            var remaining = deadline - sw.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(remaining < interval ? remaining : interval, ct);
        }

        return McpResponse.Ok(new
        {
            closed = false,
            titleOrHandle,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "window_wait_idle"), Description(
        "Wait for a window to become idle (visually stable). " +
        "Captures screenshots at intervals and waits until two consecutive captures match, " +
        "indicating the window has finished loading/animating.")]
    public async Task<string> WaitIdleAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Maximum time to wait in milliseconds.")] int timeoutMs = 10000,
        [Description("Interval between screenshot comparisons in milliseconds.")] int intervalMs = 500,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);
        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 200));
        var attempts = 0;

        byte[]? previousHash = null;

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            attempts++;

            using var screenshot = await harness.Screen.CaptureWindowAsync(titleOrHandle, ct);
            var currentHash = SHA256.HashData(screenshot.Bytes);

            if (previousHash is not null && previousHash.SequenceEqual(currentHash))
            {
                return McpResponse.Ok(new
                {
                    idle = true,
                    attempts,
                }, sw.ElapsedMilliseconds);
            }

            previousHash = currentHash;

            var remaining = deadline - sw.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(remaining < interval ? remaining : interval, ct);
        }

        return McpResponse.Ok(new
        {
            idle = false,
            attempts,
        }, sw.ElapsedMilliseconds);
    }
}
