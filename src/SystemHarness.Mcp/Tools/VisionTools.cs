using System.Globalization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;

namespace SystemHarness.Mcp.Tools;

public sealed class VisionTools(IHarness harness)
{
    [McpServerTool(Name = "vision_wait_text"), Description(
        "Poll the screen with OCR until the specified text appears or timeout is reached. " +
        "Returns immediately when text is found. Useful for waiting on UI state changes.")]
    public async Task<string> WaitTextAsync(
        [Description("Text to search for (case-insensitive substring match).")] string text,
        [Description("Maximum time to wait in milliseconds.")] int timeoutMs = 10000,
        [Description("Polling interval in milliseconds (minimum 100).")] int intervalMs = 500,
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP', 'zh-CN', 'de-DE').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(text))
            return McpResponse.Error("invalid_parameter", "text cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);
        var opts = new OcrOptions { Language = language };
        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 100));
        var attempts = 0;

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            attempts++;

            var result = await harness.Ocr.RecognizeScreenAsync(opts, ct);
            var matchLine = result.Lines.FirstOrDefault(l =>
                l.Text.Contains(text, StringComparison.OrdinalIgnoreCase));

            if (matchLine is not null)
            {
                return McpResponse.Ok(new
                {
                    found = true,
                    text = matchLine.Text,
                    bounds = new
                    {
                        matchLine.BoundingRect.X,
                        matchLine.BoundingRect.Y,
                        matchLine.BoundingRect.Width,
                        matchLine.BoundingRect.Height,
                    },
                    attempts,
                }, sw.ElapsedMilliseconds);
            }

            var remaining = deadline - sw.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(remaining < interval ? remaining : interval, ct);
        }

        return McpResponse.Ok(new
        {
            found = false,
            text = (string?)null,
            bounds = (object?)null,
            attempts,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "vision_click_text"), Description(
        "Find text on screen using OCR and click its center. " +
        "Combines screen capture, OCR, text search, and mouse click in one call. " +
        "Use 'occurrence' to select which match (1-based) when text appears multiple times.")]
    public async Task<string> ClickTextAsync(
        [Description("Text to find on screen (case-insensitive substring match).")] string text,
        [Description("Which match to click when text appears multiple times (1-based).")] int occurrence = 1,
        [Description("Mouse button: 'left' (default), 'right', or 'middle'.")] string button = "left",
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP', 'zh-CN').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(text))
            return McpResponse.Error("invalid_parameter", "text cannot be empty.", sw.ElapsedMilliseconds);
        var opts = new OcrOptions { Language = language };
        var result = await harness.Ocr.RecognizeScreenAsync(opts, ct);

        var matches = result.Lines
            .Where(l => l.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
            return McpResponse.Error("text_not_found",
                $"Text '{text}' not found on screen.", sw.ElapsedMilliseconds);

        if (occurrence < 1 || occurrence > matches.Count)
            return McpResponse.Error("occurrence_out_of_range",
                $"Occurrence {occurrence} requested but only {matches.Count} match(es) found.",
                sw.ElapsedMilliseconds);

        var match = matches[occurrence - 1];
        var cx = match.BoundingRect.CenterX;
        var cy = match.BoundingRect.CenterY;

        var btn = button.ToLowerInvariant() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left,
        };

        await harness.Mouse.ClickAsync(cx, cy, btn, ct);
        ActionLog.Record("vision_click_text", $"text='{text}', at=({cx},{cy})", sw.ElapsedMilliseconds, true);

        return McpResponse.Ok(new
        {
            clicked = true,
            text = match.Text,
            clickedAt = new { x = cx, y = cy },
            bounds = new
            {
                match.BoundingRect.X,
                match.BoundingRect.Y,
                match.BoundingRect.Width,
                match.BoundingRect.Height,
            },
            matchCount = matches.Count,
            occurrence,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "vision_read_region"), Description(
        "Read text from a rectangular region of a window using OCR. " +
        "Coordinates are relative to the window's top-left corner. " +
        "Combines window lookup, coordinate conversion, region capture, and OCR in one call.")]
    public async Task<string> ReadRegionAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("X offset relative to window top-left.")] int relX,
        [Description("Y offset relative to window top-left.")] int relY,
        [Description("Region width in pixels.")] int width,
        [Description("Region height in pixels.")] int height,
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (width <= 0 || height <= 0)
            return McpResponse.Error("invalid_dimensions", $"Width and height must be positive (got {width}x{height}).", sw.ElapsedMilliseconds);
        var windows = await harness.Window.ListAsync(ct);
        var win = ToolHelpers.FindWindow(windows, titleOrHandle);
        if (win is null)
            return McpResponse.Error("window_not_found", $"Window not found: '{titleOrHandle}'", sw.ElapsedMilliseconds);

        var absX = win.Bounds.X + relX;
        var absY = win.Bounds.Y + relY;

        var opts = new OcrOptions { Language = language };
        var result = await harness.Ocr.RecognizeRegionAsync(absX, absY, width, height, opts, ct);

        return McpResponse.Ok(new
        {
            text = result.Text,
            lineCount = result.Lines.Count,
            region = new { x = relX, y = relY, width, height },
            absoluteRegion = new { x = absX, y = absY, width, height },
            window = new { handle = win.Handle.ToString(CultureInfo.InvariantCulture), win.Title },
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "vision_wait_change"), Description(
        "Wait for a visible change in a window or screen region. " +
        "Captures an initial screenshot, then polls until the image changes or timeout is reached. " +
        "Useful for detecting UI transitions, loading completion, or animation end.")]
    public async Task<string> WaitChangeAsync(
        [Description("Optional window to monitor (title substring or handle). Omit for full screen.")] string? titleOrHandle = null,
        [Description("Optional region X (all four region params required together).")] int? regionX = null,
        [Description("Optional region Y.")] int? regionY = null,
        [Description("Optional region width.")] int? regionW = null,
        [Description("Optional region height.")] int? regionH = null,
        [Description("Maximum time to wait in milliseconds.")] int timeoutMs = 10000,
        [Description("Polling interval in milliseconds (minimum 100).")] int intervalMs = 500,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 100));
        var attempts = 0;

        // Capture initial baseline
        var initialHash = await CaptureHash(titleOrHandle, regionX, regionY, regionW, regionH, ct);

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var remaining = deadline - sw.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(remaining < interval ? remaining : interval, ct);
            attempts++;

            var currentHash = await CaptureHash(titleOrHandle, regionX, regionY, regionW, regionH, ct);

            if (!initialHash.SequenceEqual(currentHash))
            {
                return McpResponse.Ok(new
                {
                    changed = true,
                    attempts,
                }, sw.ElapsedMilliseconds);
            }
        }

        return McpResponse.Ok(new
        {
            changed = false,
            attempts,
        }, sw.ElapsedMilliseconds);
    }

    private async Task<byte[]> CaptureHash(
        string? titleOrHandle, int? x, int? y, int? w, int? h, CancellationToken ct)
    {
        Screenshot screenshot;
        if (x.HasValue && y.HasValue && w.HasValue && h.HasValue)
            screenshot = await harness.Screen.CaptureRegionAsync(x.Value, y.Value, w.Value, h.Value, ct);
        else if (titleOrHandle is not null)
            screenshot = await harness.Screen.CaptureWindowAsync(titleOrHandle, ct);
        else
            screenshot = await harness.Screen.CaptureAsync(ct: ct);

        using (screenshot)
            return SHA256.HashData(screenshot.Bytes);
    }

    [McpServerTool(Name = "vision_find_text"), Description(
        "Capture the screen and find all lines containing the specified text. " +
        "Returns matching lines with their bounding rectangles (screen coordinates).")]
    public async Task<string> FindTextAsync(
        [Description("Text to search for (case-insensitive substring match).")] string text,
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(text))
            return McpResponse.Error("invalid_parameter", "text cannot be empty.", sw.ElapsedMilliseconds);
        var opts = new OcrOptions { Language = language };
        var result = await harness.Ocr.RecognizeScreenAsync(opts, ct);

        var matches = result.Lines
            .Where(l => l.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
            .Select(l => new
            {
                l.Text,
                bounds = new
                {
                    l.BoundingRect.X,
                    l.BoundingRect.Y,
                    l.BoundingRect.Width,
                    l.BoundingRect.Height,
                },
            })
            .ToArray();

        return McpResponse.Items(matches, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "vision_snapshot"), Description(
        "Capture a labeled screenshot and save to a temp file. " +
        "Returns the file path and SHA256 hash for later comparison. " +
        "Use before and after actions to detect visual changes by comparing hashes.")]
    public async Task<string> SnapshotAsync(
        [Description("Label for the snapshot file name (alphanumeric, hyphens, underscores).")] string label = "snapshot",
        [Description("Optional window to capture (title substring or handle). Omit for full screen.")] string? titleOrHandle = null,
        [Description("Optional region X (all four region params required together).")] int? regionX = null,
        [Description("Optional region Y.")] int? regionY = null,
        [Description("Optional region width.")] int? regionW = null,
        [Description("Optional region height.")] int? regionH = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        Screenshot screenshot;
        if (regionX.HasValue && regionY.HasValue && regionW.HasValue && regionH.HasValue)
            screenshot = await harness.Screen.CaptureRegionAsync(regionX.Value, regionY.Value, regionW.Value, regionH.Value, ct);
        else if (titleOrHandle is not null)
            screenshot = await harness.Screen.CaptureWindowAsync(titleOrHandle, ct);
        else
            screenshot = await harness.Screen.CaptureAsync(ct: ct);

        using (screenshot)
        {
            var hash = Convert.ToHexString(SHA256.HashData(screenshot.Bytes));
            var safeName = new string(label.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            if (safeName.Length == 0) safeName = "snapshot";
            var path = Path.Combine(Path.GetTempPath(), $"harness-{safeName}-{DateTime.Now:HHmmss}.png");
            await File.WriteAllBytesAsync(path, screenshot.Bytes, ct);

            return McpResponse.Ok(new
            {
                path,
                hash,
                width = screenshot.Width,
                height = screenshot.Height,
                sizeBytes = screenshot.Bytes.Length,
                label,
            }, sw.ElapsedMilliseconds);
        }
    }

    [McpServerTool(Name = "vision_click_and_verify"), Description(
        "Click at screen coordinates and verify the click caused a visible change. " +
        "Captures a screenshot before and after the click, compares them. " +
        "Returns whether a change was detected and the click coordinates.")]
    public async Task<string> ClickAndVerifyAsync(
        [Description("Absolute screen X coordinate to click.")] int x,
        [Description("Absolute screen Y coordinate to click.")] int y,
        [Description("Mouse button: 'left' (default), 'right', or 'middle'.")] string button = "left",
        [Description("Delay before verification screenshot in milliseconds.")] int verifyDelayMs = 500,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Capture before
        using var before = await harness.Screen.CaptureAsync(ct: ct);
        var hashBefore = SHA256.HashData(before.Bytes);

        // Click
        var btn = button.ToLowerInvariant() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left,
        };
        await harness.Mouse.ClickAsync(x, y, btn, ct);
        await Task.Delay(Math.Max(verifyDelayMs, 100), ct);

        // Capture after
        using var after = await harness.Screen.CaptureAsync(ct: ct);
        var hashAfter = SHA256.HashData(after.Bytes);

        var changed = !hashBefore.SequenceEqual(hashAfter);
        ActionLog.Record("vision_click_and_verify", $"({x},{y}) {button}, changed={changed}", sw.ElapsedMilliseconds, true);

        return McpResponse.Ok(new
        {
            clicked = true,
            changed,
            clickedAt = new { x, y },
            button,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "vision_type_and_verify"), Description(
        "Type text into the focused element and verify the text appeared on screen via OCR. " +
        "Optionally specify screen coordinates to verify in a specific region.")]
    public async Task<string> TypeAndVerifyAsync(
        [Description("Text to type.")] string text,
        [Description("Optional X for OCR verification region.")] int? verifyX = null,
        [Description("Optional Y for OCR verification region.")] int? verifyY = null,
        [Description("Optional width for OCR verification region.")] int? verifyW = null,
        [Description("Optional height for OCR verification region.")] int? verifyH = null,
        [Description("Delay before OCR verification in milliseconds.")] int verifyDelayMs = 300,
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Type the text
        await harness.Keyboard.TypeAsync(text, ct: ct);
        await Task.Delay(Math.Max(verifyDelayMs, 100), ct);

        // Verify via OCR
        var opts = new OcrOptions { Language = language };
        OcrResult ocrResult;
        if (verifyX.HasValue && verifyY.HasValue && verifyW.HasValue && verifyH.HasValue)
            ocrResult = await harness.Ocr.RecognizeRegionAsync(verifyX.Value, verifyY.Value, verifyW.Value, verifyH.Value, opts, ct);
        else
            ocrResult = await harness.Ocr.RecognizeScreenAsync(opts, ct);

        var verified = ocrResult.Lines.Any(l =>
            l.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        ActionLog.Record("vision_type_and_verify", $"text='{text}', verified={verified}", sw.ElapsedMilliseconds, true);

        return McpResponse.Ok(new
        {
            typed = text,
            verified,
            ocrText = ocrResult.Text,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "vision_find_image"), Description(
        "Find all occurrences of a template image within a screenshot. " +
        "Uses Normalized Cross-Correlation (NCC) for illumination-robust matching. " +
        "Returns matches sorted by confidence (highest first) with bounding boxes and center coordinates. " +
        "Useful for finding icons, buttons, or UI elements that lack accessibility labels.")]
    public async Task<string> FindImageAsync(
        [Description("Path to the template image file (PNG recommended).")] string templatePath,
        [Description("Optional window to search within (title substring or handle). Omit for full screen.")] string? titleOrHandle = null,
        [Description("Confidence threshold 0.0-1.0. Higher = stricter matching. Default 0.8 recommended.")] double threshold = 0.8,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(templatePath))
            return McpResponse.Error("invalid_parameter", "templatePath cannot be empty.", sw.ElapsedMilliseconds);
        if (!File.Exists(templatePath))
            return McpResponse.Error("file_not_found", $"Template file not found: '{templatePath}'", sw.ElapsedMilliseconds);

        Screenshot screenshot;
        if (titleOrHandle is not null)
            screenshot = await harness.Screen.CaptureWindowAsync(titleOrHandle, ct);
        else
            screenshot = await harness.Screen.CaptureAsync(ct: ct);

        using (screenshot)
        {
            var matches = await harness.TemplateMatcher.FindAsync(screenshot, templatePath, threshold, ct);

            ActionLog.Record("vision_find_image", $"template={Path.GetFileName(templatePath)}, matches={matches.Count}",
                sw.ElapsedMilliseconds, true);

            return McpResponse.Items(matches.Select(m => new
            {
                m.X, m.Y, m.Width, m.Height,
                centerX = m.CenterX,
                centerY = m.CenterY,
                m.Confidence,
            }).ToArray(), sw.ElapsedMilliseconds);
        }
    }

    [McpServerTool(Name = "vision_click_image"), Description(
        "Find a template image on screen and click its center. " +
        "Combines screen capture, template matching (NCC), and mouse click in one call. " +
        "Use 'occurrence' to select which match (1-based) when multiple matches are found. " +
        "Useful for clicking icons, images, or UI elements that lack accessibility labels.")]
    public async Task<string> ClickImageAsync(
        [Description("Path to the template image file (PNG recommended).")] string templatePath,
        [Description("Which match to click when multiple found (1-based).")] int occurrence = 1,
        [Description("Mouse button: 'left' (default), 'right', or 'middle'.")] string button = "left",
        [Description("Optional window to search within (title substring or handle). Omit for full screen.")] string? titleOrHandle = null,
        [Description("Confidence threshold 0.0-1.0. Higher = stricter matching. Default 0.8 recommended.")] double threshold = 0.8,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(templatePath))
            return McpResponse.Error("invalid_parameter", "templatePath cannot be empty.", sw.ElapsedMilliseconds);
        if (!File.Exists(templatePath))
            return McpResponse.Error("file_not_found", $"Template file not found: '{templatePath}'", sw.ElapsedMilliseconds);

        Screenshot screenshot;
        int offsetX = 0, offsetY = 0;
        if (titleOrHandle is not null)
        {
            var windows = await harness.Window.ListAsync(ct);
            var win = ToolHelpers.FindWindow(windows, titleOrHandle);
            if (win is not null)
            {
                offsetX = win.Bounds.X;
                offsetY = win.Bounds.Y;
            }
            screenshot = await harness.Screen.CaptureWindowAsync(titleOrHandle, ct);
        }
        else
        {
            screenshot = await harness.Screen.CaptureAsync(ct: ct);
        }

        IReadOnlyList<TemplateMatchResult> matches;
        using (screenshot)
            matches = await harness.TemplateMatcher.FindAsync(screenshot, templatePath, threshold, ct);

        if (matches.Count == 0)
            return McpResponse.Error("image_not_found",
                $"Template '{Path.GetFileName(templatePath)}' not found on screen (threshold={threshold}).",
                sw.ElapsedMilliseconds);

        if (occurrence < 1 || occurrence > matches.Count)
            return McpResponse.Error("occurrence_out_of_range",
                $"Occurrence {occurrence} requested but only {matches.Count} match(es) found.",
                sw.ElapsedMilliseconds);

        var match = matches[occurrence - 1];
        var clickX = match.CenterX + offsetX;
        var clickY = match.CenterY + offsetY;

        var btn = button.ToLowerInvariant() switch
        {
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => MouseButton.Left,
        };

        await harness.Mouse.ClickAsync(clickX, clickY, btn, ct);

        ActionLog.Record("vision_click_image", $"template={Path.GetFileName(templatePath)}, at=({clickX},{clickY})",
            sw.ElapsedMilliseconds, true);

        return McpResponse.Ok(new
        {
            clicked = true,
            clickedAt = new { x = clickX, y = clickY },
            match = new
            {
                match.X, match.Y, match.Width, match.Height,
                centerX = match.CenterX,
                centerY = match.CenterY,
                match.Confidence,
            },
            matchCount = matches.Count,
            occurrence,
            button,
        }, sw.ElapsedMilliseconds);
    }
}
