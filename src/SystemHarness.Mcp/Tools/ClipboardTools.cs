using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class ClipboardTools(IHarness harness)
{
    [McpServerTool(Name = "clipboard_get_text"), Description("Get the current text content from the clipboard.")]
    public async Task<string> GetTextAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var text = await harness.Clipboard.GetTextAsync(ct);
        return text is not null
            ? McpResponse.Content(text, "text", sw.ElapsedMilliseconds)
            : McpResponse.Ok(new { content = (string?)null, format = "text" }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "clipboard_set_text"), Description("Set text content to the clipboard.")]
    public async Task<string> SetTextAsync(
        [Description("Text to copy to the clipboard.")] string text, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await harness.Clipboard.SetTextAsync(text, ct);
        ActionLog.Record("clipboard_set_text", $"len={text.Length}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm("Text copied to clipboard.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "clipboard_get_html"), Description("Get HTML content from the clipboard if available.")]
    public async Task<string> GetHtmlAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var html = await harness.Clipboard.GetHtmlAsync(ct);
        return html is not null
            ? McpResponse.Content(html, "html", sw.ElapsedMilliseconds)
            : McpResponse.Ok(new { content = (string?)null, format = "html" }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "clipboard_get_image"), Description(
        "Get image data from the clipboard and save to a temp file. " +
        "Returns the file path and size if an image is available.")]
    public async Task<string> GetImageAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var imageData = await harness.Clipboard.GetImageAsync(ct);
        if (imageData is null || imageData.Length == 0)
            return McpResponse.Ok(new { hasImage = false }, sw.ElapsedMilliseconds);

        var path = Path.Combine(Path.GetTempPath(), $"harness-clipboard-{DateTime.Now:HHmmss}.png");
        await File.WriteAllBytesAsync(path, imageData, ct);

        return McpResponse.Ok(new
        {
            hasImage = true,
            path,
            sizeBytes = imageData.Length,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "clipboard_set_image"), Description(
        "Set image data to the clipboard from a file path.")]
    public async Task<string> SetImageAsync(
        [Description("Path to image file (PNG, JPG, BMP, etc.).")] string path,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        if (!File.Exists(path))
            return McpResponse.Error("file_not_found", $"Image file not found: '{path}'", sw.ElapsedMilliseconds);

        var imageData = await File.ReadAllBytesAsync(path, ct);
        await harness.Clipboard.SetImageAsync(imageData, ct);
        ActionLog.Record("clipboard_set_image", $"path={path}, bytes={imageData.Length}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Image set to clipboard ({imageData.Length} bytes).", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "clipboard_set_html"), Description("Set HTML content to the clipboard.")]
    public async Task<string> SetHtmlAsync(
        [Description("HTML content to copy to the clipboard.")] string html,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrEmpty(html))
            return McpResponse.Error("invalid_parameter", "html cannot be empty.", sw.ElapsedMilliseconds);
        await harness.Clipboard.SetHtmlAsync(html, ct);
        ActionLog.Record("clipboard_set_html", $"len={html.Length}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm("HTML copied to clipboard.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "clipboard_set_files"), Description("Set a list of file paths to the clipboard (file drop list for paste operations).")]
    public async Task<string> SetFilesAsync(
        [Description("Comma-separated file paths to set in the clipboard drop list.")] string paths,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(paths))
            return McpResponse.Error("invalid_parameter", "paths cannot be empty.", sw.ElapsedMilliseconds);
        var pathList = paths.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (pathList.Length == 0)
            return McpResponse.Error("invalid_parameter", "No valid file paths provided.", sw.ElapsedMilliseconds);
        await harness.Clipboard.SetFileDropListAsync(pathList, ct);
        ActionLog.Record("clipboard_set_files", $"count={pathList.Length}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Set {pathList.Length} file(s) to clipboard drop list.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "clipboard_get_files"), Description(
        "Get the list of files from the clipboard (file drop list).")]
    public async Task<string> GetFilesAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var files = await harness.Clipboard.GetFileDropListAsync(ct);
        if (files is null || files.Count == 0)
            return McpResponse.Ok(new { hasFiles = false, files = Array.Empty<string>() }, sw.ElapsedMilliseconds);

        return McpResponse.Ok(new
        {
            hasFiles = true,
            files,
            count = files.Count,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "clipboard_get_formats"), Description(
        "Get the list of available clipboard formats (text, html, image, files, etc.).")]
    public async Task<string> GetFormatsAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var formats = await harness.Clipboard.GetAvailableFormatsAsync(ct);
        return McpResponse.Items(formats.ToArray(), sw.ElapsedMilliseconds);
    }
}
