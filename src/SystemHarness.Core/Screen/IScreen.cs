namespace SystemHarness;

/// <summary>
/// Screen capture — full screen, region, or specific window.
/// </summary>
public interface IScreen
{
    /// <summary>
    /// Captures the entire primary display (or active monitor).
    /// </summary>
    Task<Screenshot> CaptureAsync(CaptureOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Captures a specific rectangular region of the screen.
    /// </summary>
    Task<Screenshot> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken ct = default);

    /// <summary>
    /// Captures a specific window by title or handle.
    /// </summary>
    Task<Screenshot> CaptureWindowAsync(string titleOrHandle, CancellationToken ct = default);

    // --- Phase 8 Extension ---

    /// <summary>
    /// Captures a specific monitor by its index.
    /// </summary>
    Task<Screenshot> CaptureMonitorAsync(int monitorIndex, CaptureOptions? options = null, CancellationToken ct = default)
        => throw new NotSupportedException("CaptureMonitorAsync is not supported by this implementation.");

    // --- Convenience overloads (DIM) ---

    /// <summary>
    /// Captures a specific rectangular region with custom capture options (e.g., disable resize for OCR accuracy).
    /// Implementations must override this to honor the options parameter.
    /// </summary>
    Task<Screenshot> CaptureRegionAsync(int x, int y, int width, int height,
        CaptureOptions? options, CancellationToken ct = default)
        => CaptureRegionAsync(x, y, width, height, ct);

    /// <summary>
    /// Captures a specific window with custom capture options (e.g., disable resize for OCR accuracy).
    /// Default implementation delegates to the base overload (ignoring options).
    /// </summary>
    Task<Screenshot> CaptureWindowAsync(string titleOrHandle,
        CaptureOptions? options, CancellationToken ct = default)
        => CaptureWindowAsync(titleOrHandle, ct);

    /// <summary>
    /// Captures a rectangular region relative to a window's client area.
    /// </summary>
    Task<Screenshot> CaptureWindowRegionAsync(string titleOrHandle,
        int relativeX, int relativeY, int width, int height,
        CaptureOptions? options = null, CancellationToken ct = default)
        => throw new NotSupportedException("CaptureWindowRegionAsync is not supported by this implementation.");
}
