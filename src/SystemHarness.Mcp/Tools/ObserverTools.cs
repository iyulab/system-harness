using System.Globalization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class ObserverTools(IObserver observer)
{
    [McpServerTool(Name = "observe_window"), Description(
        "Observe a window's complete state in one call — screenshot, accessibility tree, and OCR. " +
        "Returns a unified view combining all observation channels. " +
        "This is the primary tool for AI agents to understand the current UI state.")]
    public async Task<string> ObserveAsync(
        [Description("Window title (substring match) or handle string.")] string titleOrHandle,
        [Description("Include a screenshot of the window.")] bool includeScreenshot = true,
        [Description("Include the accessibility tree.")] bool includeAccessibilityTree = true,
        [Description("Include OCR text recognition (slower).")] bool includeOcr = false,
        [Description("Maximum depth for accessibility tree traversal.")] int maxDepth = 5,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (maxDepth < 1)
            return McpResponse.Error("invalid_parameter", $"maxDepth must be >= 1 (got {maxDepth}).", sw.ElapsedMilliseconds);

        var options = new ObserveOptions
        {
            IncludeScreenshot = includeScreenshot,
            IncludeAccessibilityTree = includeAccessibilityTree,
            IncludeOcr = includeOcr,
            AccessibilityTreeMaxDepth = maxDepth,
        };

        using var observation = await observer.ObserveAsync(titleOrHandle, options, ct);

        // Build response — save screenshot to temp if present
        string? screenshotPath = null;
        int? screenshotWidth = null;
        int? screenshotHeight = null;
        if (observation.Screenshot is not null)
        {
            screenshotPath = Path.Combine(Path.GetTempPath(), $"harness-observe-{DateTime.Now:HHmmss}.png");
            await observation.Screenshot.SaveAsync(screenshotPath, ct);
            screenshotWidth = observation.Screenshot.Width;
            screenshotHeight = observation.Screenshot.Height;
        }

        return McpResponse.Ok(new
        {
            window = observation.WindowInfo is not null ? new
            {
                handle = observation.WindowInfo.Handle.ToString(CultureInfo.InvariantCulture),
                observation.WindowInfo.Title,
                observation.WindowInfo.ProcessId,
                bounds = new
                {
                    observation.WindowInfo.Bounds.X,
                    observation.WindowInfo.Bounds.Y,
                    observation.WindowInfo.Bounds.Width,
                    observation.WindowInfo.Bounds.Height,
                },
                state = observation.WindowInfo.State.ToString(),
            } : null,
            screenshot = screenshotPath is not null ? new
            {
                path = screenshotPath,
                width = screenshotWidth,
                height = screenshotHeight,
            } : null,
            accessibilityTree = observation.AccessibilityTree is not null ? new
            {
                root = FormatElement(observation.AccessibilityTree),
                elementCount = CountElements(observation.AccessibilityTree),
            } : null,
            ocr = observation.OcrText is not null ? new
            {
                text = observation.OcrText.Text,
                lineCount = observation.OcrText.Lines.Count,
            } : null,
            timestamp = observation.Timestamp.ToString("O"),
        }, sw.ElapsedMilliseconds);
    }

    private static object FormatElement(UIElement element)
    {
        return new
        {
            element.Name,
            element.AutomationId,
            controlType = element.ControlType.ToString(),
            element.Value,
            element.IsEnabled,
            element.IsOffscreen,
            bounds = new
            {
                element.BoundingRectangle.X,
                element.BoundingRectangle.Y,
                element.BoundingRectangle.Width,
                element.BoundingRectangle.Height,
            },
            children = element.Children.Select(FormatElement).ToArray(),
        };
    }

    private static int CountElements(UIElement root)
    {
        var count = 1;
        foreach (var child in root.Children)
            count += CountElements(child);
        return count;
    }
}
