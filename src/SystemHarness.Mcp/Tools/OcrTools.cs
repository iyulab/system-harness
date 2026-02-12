using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class OcrTools(IHarness harness)
{
    [McpServerTool(Name = "ocr_read"), Description("Capture the full screen and extract all visible text using OCR.")]
    public async Task<string> ScreenAsync(
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP', 'zh-CN').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await harness.Ocr.RecognizeScreenAsync(new OcrOptions { Language = language }, ct);
        return McpResponse.Content(result.Text, "text", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ocr_read_region"), Description("Capture a screen region and extract text using OCR.")]
    public async Task<string> RegionAsync(
        [Description("Top-left X coordinate of the region.")] int x,
        [Description("Top-left Y coordinate of the region.")] int y,
        [Description("Region width in pixels.")] int width,
        [Description("Region height in pixels.")] int height,
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP', 'zh-CN').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (width <= 0 || height <= 0)
            return McpResponse.Error("invalid_dimensions", $"Width and height must be positive (got {width}x{height}).", sw.ElapsedMilliseconds);
        var result = await harness.Ocr.RecognizeRegionAsync(x, y, width, height, new OcrOptions { Language = language }, ct);
        return McpResponse.Content(result.Text, "text", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ocr_read_detailed"), Description("Capture the full screen and extract text with line count and bounding rectangles per line.")]
    public async Task<string> ScreenDetailedAsync(
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP', 'zh-CN').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await harness.Ocr.RecognizeScreenAsync(new OcrOptions { Language = language }, ct);
        return McpResponse.Ok(new
        {
            lineCount = result.Lines.Count,
            lines = result.Lines.Select(l => new
            {
                l.Text,
                bounds = new { l.BoundingRect.X, l.BoundingRect.Y, l.BoundingRect.Width, l.BoundingRect.Height },
            }),
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ocr_read_image"), Description("Extract text from an image file using OCR.")]
    public async Task<string> ImageAsync(
        [Description("Path to the image file (PNG, JPG, BMP, etc.).")] string path,
        [Description("BCP-47 language for OCR (e.g., 'en-US', 'ko-KR', 'ja-JP', 'zh-CN').")] string language = "en-US",
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        if (!File.Exists(path))
            return McpResponse.Error("file_not_found", $"Image file not found: '{path}'", sw.ElapsedMilliseconds);
        var imageData = await File.ReadAllBytesAsync(path, ct);
        var result = await harness.Ocr.RecognizeImageAsync(imageData, new OcrOptions { Language = language }, ct);
        return McpResponse.Content(result.Text, "text", sw.ElapsedMilliseconds);
    }
}
