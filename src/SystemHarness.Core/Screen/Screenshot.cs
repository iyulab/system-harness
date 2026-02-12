namespace SystemHarness;

/// <summary>
/// A captured screenshot with encoded image data.
/// </summary>
public sealed class Screenshot : IDisposable
{
    /// <summary>
    /// Raw image bytes (PNG or JPEG encoded).
    /// </summary>
    public required byte[] Bytes { get; init; }

    /// <summary>
    /// Base64-encoded image data, ready for LLM vision APIs.
    /// </summary>
    public string Base64 => Convert.ToBase64String(Bytes);

    /// <summary>
    /// MIME type of the image (e.g., "image/png", "image/jpeg").
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Timestamp when the capture was taken.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Saves the screenshot to a file.
    /// </summary>
    public async Task SaveAsync(string path, CancellationToken ct = default)
    {
        await File.WriteAllBytesAsync(path, Bytes, ct);
    }

    public void Dispose()
    {
        // Future: return pooled buffers
    }
}
