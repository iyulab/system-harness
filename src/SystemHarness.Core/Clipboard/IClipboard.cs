namespace SystemHarness;

/// <summary>
/// System clipboard access — get/set text, images, HTML, file drop lists, and format enumeration.
/// </summary>
public interface IClipboard
{
    /// <summary>
    /// Gets the current text content from the clipboard, or null if no text is available.
    /// </summary>
    Task<string?> GetTextAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets text content to the clipboard.
    /// </summary>
    Task SetTextAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Gets the current image from the clipboard as PNG-encoded bytes, or null if no image is available.
    /// </summary>
    Task<byte[]?> GetImageAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets an image to the clipboard from PNG-encoded bytes.
    /// </summary>
    Task SetImageAsync(byte[] imageData, CancellationToken ct = default);

    // --- Phase 9 Extensions (DIM for backward compatibility) ---

    /// <summary>
    /// Gets HTML content from the clipboard (CF_HTML format).
    /// </summary>
    Task<string?> GetHtmlAsync(CancellationToken ct = default)
        => throw new NotSupportedException("GetHtmlAsync is not supported by this implementation.");

    /// <summary>
    /// Sets HTML content to the clipboard.
    /// </summary>
    Task SetHtmlAsync(string html, CancellationToken ct = default)
        => throw new NotSupportedException("SetHtmlAsync is not supported by this implementation.");

    /// <summary>
    /// Gets the list of file paths from a file drop clipboard operation (CF_HDROP).
    /// </summary>
    Task<IReadOnlyList<string>?> GetFileDropListAsync(CancellationToken ct = default)
        => throw new NotSupportedException("GetFileDropListAsync is not supported by this implementation.");

    /// <summary>
    /// Sets a list of file paths for file drop clipboard operation.
    /// </summary>
    Task SetFileDropListAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
        => throw new NotSupportedException("SetFileDropListAsync is not supported by this implementation.");

    /// <summary>
    /// Gets the list of clipboard format names currently available.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableFormatsAsync(CancellationToken ct = default)
        => throw new NotSupportedException("GetAvailableFormatsAsync is not supported by this implementation.");
}
