using System.Globalization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class ReportTools(IHarness harness)
{
    [McpServerTool(Name = "report_get_desktop"), Description(
        "Get a snapshot of the full desktop state in one call. " +
        "Returns monitors, visible windows (with foreground indicator), and mouse position.")]
    public async Task<string> GetDesktopAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var monitorsTask = harness.Display.GetMonitorsAsync(ct);
        var windowsTask = harness.Window.ListAsync(ct);
        var foregroundTask = harness.Window.GetForegroundAsync(ct);
        var mouseTask = harness.Mouse.GetPositionAsync(ct);

        await Task.WhenAll(monitorsTask, windowsTask, foregroundTask, mouseTask);

        var monitors = await monitorsTask;
        var windows = await windowsTask;
        var foreground = await foregroundTask;
        var (mx, my) = await mouseTask;

        var fgHandle = foreground?.Handle ?? 0;

        return McpResponse.Ok(new
        {
            monitors = monitors.Select(m => new
            {
                m.Index, m.Name, m.IsPrimary,
                bounds = new { m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height },
                m.ScaleFactor,
            }).ToArray(),
            windows = windows.Select(w => new
            {
                handle = w.Handle.ToString(CultureInfo.InvariantCulture),
                w.Title,
                w.ProcessId,
                bounds = new { w.Bounds.X, w.Bounds.Y, w.Bounds.Width, w.Bounds.Height },
                state = w.State.ToString(),
                isForeground = w.Handle == fgHandle,
            }).ToArray(),
            mouse = new { x = mx, y = my },
            windowCount = windows.Count,
            monitorCount = monitors.Count,
        }, sw.ElapsedMilliseconds);
    }

    private static readonly UIControlType[] ClickableTypes =
    [
        UIControlType.Button, UIControlType.Hyperlink, UIControlType.MenuItem,
        UIControlType.CheckBox, UIControlType.RadioButton, UIControlType.TabItem,
        UIControlType.SplitButton, UIControlType.ListItem, UIControlType.TreeItem,
    ];

    private static readonly UIControlType[] InputTypes =
    [
        UIControlType.Edit, UIControlType.ComboBox, UIControlType.Spinner,
        UIControlType.Slider,
    ];

    [McpServerTool(Name = "report_get_screen"), Description(
        "Get a complete screen context for a window in one call. " +
        "Returns a screenshot file path, OCR-extracted text, and detected UI elements (clickables + inputs). " +
        "This is the 'see everything at once' tool — reduces 3+ separate calls to 1.")]
    public async Task<string> GetScreenAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);

        // Capture screenshot and get accessibility tree in parallel
        var screenshotTask = harness.Screen.CaptureWindowAsync(titleOrHandle, ct);
        var treeTask = harness.UIAutomation.GetAccessibilityTreeAsync(titleOrHandle, 5, ct);

        await Task.WhenAll(screenshotTask, treeTask);

        using var screenshot = await screenshotTask;
        var tree = await treeTask;

        // OCR the captured image bytes (no double-capture)
        var opts = new OcrOptions { Language = language };
        var ocrResult = await harness.Ocr.RecognizeImageAsync(screenshot.Bytes, opts, ct);

        // Save screenshot to temp
        var path = Path.Combine(Path.GetTempPath(), $"harness-screen-{DateTime.Now:HHmmss}.png");
        await screenshot.SaveAsync(path, ct);

        // Flatten UI tree → extract clickables and inputs
        var allElements = FlattenElements(tree).Where(e => e.IsEnabled && !e.IsOffscreen).ToList();

        var clickables = allElements
            .Where(e => ClickableTypes.Contains(e.ControlType))
            .Select(e => new
            {
                e.Name, e.AutomationId, controlType = e.ControlType.ToString(),
                bounds = new { e.BoundingRectangle.X, e.BoundingRectangle.Y, e.BoundingRectangle.Width, e.BoundingRectangle.Height },
            })
            .ToArray();

        var inputs = allElements
            .Where(e => InputTypes.Contains(e.ControlType))
            .Select(e => new
            {
                e.Name, e.AutomationId, controlType = e.ControlType.ToString(),
                e.Value,
                bounds = new { e.BoundingRectangle.X, e.BoundingRectangle.Y, e.BoundingRectangle.Width, e.BoundingRectangle.Height },
            })
            .ToArray();

        return McpResponse.Ok(new
        {
            screenshot = new { path, screenshot.Width, screenshot.Height },
            ocr = new
            {
                text = ocrResult.Text,
                lineCount = ocrResult.Lines.Count,
            },
            ui = new
            {
                clickableCount = clickables.Length,
                clickables,
                inputCount = inputs.Length,
                inputs,
            },
        }, sw.ElapsedMilliseconds);
    }

    private static IEnumerable<UIElement> FlattenElements(UIElement root)
    {
        yield return root;
        foreach (var child in root.Children)
            foreach (var descendant in FlattenElements(child))
                yield return descendant;
    }

    [McpServerTool(Name = "report_get_window"), Description(
        "Get a detailed report for a specific window by title or handle. " +
        "Returns window info, bounds, state, process info, and child windows.")]
    public async Task<string> GetWindowAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);

        // Get window list to find the target
        var windows = await harness.Window.ListAsync(ct);
        var target = ToolHelpers.FindWindow(windows, titleOrHandle);
        if (target is null)
            return McpResponse.Error("window_not_found", $"Window not found: '{titleOrHandle}'", sw.ElapsedMilliseconds);

        // Parallel fetch: child windows + state + monitor
        var childrenTask = harness.Window.GetChildWindowsAsync(titleOrHandle, ct);
        var stateTask = harness.Window.GetStateAsync(titleOrHandle, ct);
        var monitorTask = harness.Display.GetMonitorForWindowAsync(titleOrHandle, ct);

        await Task.WhenAll(childrenTask, stateTask, monitorTask);

        var children = await childrenTask;
        var state = await stateTask;
        var monitor = await monitorTask;

        return McpResponse.Ok(new
        {
            handle = target.Handle.ToString(CultureInfo.InvariantCulture),
            target.Title,
            target.ProcessId,
            target.ClassName,
            state = state.ToString(),
            bounds = new { target.Bounds.X, target.Bounds.Y, target.Bounds.Width, target.Bounds.Height },
            monitor = new { monitor.Index, monitor.Name },
            children = children.Select(c => new
            {
                handle = c.Handle.ToString(CultureInfo.InvariantCulture),
                c.Title,
                c.ClassName,
            }).ToArray(),
            childCount = children.Count,
        }, sw.ElapsedMilliseconds);
    }

}
