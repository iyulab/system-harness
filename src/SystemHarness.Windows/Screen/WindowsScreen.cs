using System.Runtime.InteropServices;
using SkiaSharp;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IScreen"/>.
/// Uses DXGI Desktop Duplication (GPU-accelerated) with GDI BitBlt fallback.
/// Supports cursor overlay compositing.
/// </summary>
public sealed class WindowsScreen : IScreen, IDisposable
{
    private readonly object _dxgiLock = new();
    private DxgiScreenCapturer? _dxgiCapturer;
    private volatile bool _dxgiAttempted;

    public WindowsScreen()
    {
        DpiInitializer.EnsureDpiAwareness();
    }

    public Task<Screenshot> CaptureAsync(CaptureOptions? options = null, CancellationToken ct = default)
    {
        options ??= new CaptureOptions();

        return Task.Run(() =>
        {
            // Try DXGI Desktop Duplication first (GPU-accelerated)
            var dxgiPixels = TryCaptureDxgi(out var dxgiW, out var dxgiH);
            if (dxgiPixels is not null)
            {
                if (options.IncludeCursor)
                    CompositeCursor(dxgiPixels, dxgiW, dxgiH, 0, 0);
                return EncodeScreenshot(dxgiPixels, dxgiW, dxgiH, options);
            }

            // Fallback to GDI BitBlt
            var screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
            var screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
            return CaptureGdiWithCursor(0, 0, screenWidth, screenHeight, options);
        }, ct);
    }

    public Task<Screenshot> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken ct = default)
    {
        // Region captures preserve original dimensions (no default 1024x768 resize)
        return Task.Run(() => CaptureGdiWithCursor(x, y, width, height,
            new CaptureOptions { TargetWidth = null, TargetHeight = null }), ct);
    }

    public Task<Screenshot> CaptureRegionAsync(int x, int y, int width, int height,
        CaptureOptions? options, CancellationToken ct = default)
    {
        // For region captures, preserve original dimensions unless explicitly set
        var opts = options ?? new CaptureOptions { TargetWidth = null, TargetHeight = null };
        return Task.Run(() => CaptureGdiWithCursor(x, y, width, height, opts), ct);
    }

    public Task<Screenshot> CaptureWindowAsync(string titleOrHandle, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var hwnd = await FindWindowHandle(titleOrHandle, ct);

            PInvoke.GetWindowRect(hwnd, out var rect);
            var width = rect.Width;
            var height = rect.Height;

            if (width <= 0 || height <= 0)
                throw new HarnessException($"Window has invalid bounds: {width}x{height}");

            return CaptureGdiWithCursor(rect.X, rect.Y, width, height, new CaptureOptions());
        }, ct);
    }

    public Task<Screenshot> CaptureWindowAsync(string titleOrHandle,
        CaptureOptions? options, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var hwnd = await FindWindowHandle(titleOrHandle, ct);

            PInvoke.GetWindowRect(hwnd, out var rect);
            var width = rect.Width;
            var height = rect.Height;

            if (width <= 0 || height <= 0)
                throw new HarnessException($"Window has invalid bounds: {width}x{height}");

            return CaptureGdiWithCursor(rect.X, rect.Y, width, height, options ?? new CaptureOptions());
        }, ct);
    }

    public Task<Screenshot> CaptureWindowRegionAsync(string titleOrHandle,
        int relativeX, int relativeY, int width, int height,
        CaptureOptions? options = null, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var hwnd = await FindWindowHandle(titleOrHandle, ct);

            PInvoke.GetWindowRect(hwnd, out var rect);
            var absX = rect.X + relativeX;
            var absY = rect.Y + relativeY;

            return CaptureGdiWithCursor(absX, absY, width, height, options ?? new CaptureOptions());
        }, ct);
    }

    private byte[]? TryCaptureDxgi(out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!_dxgiAttempted)
        {
            lock (_dxgiLock)
            {
                if (!_dxgiAttempted)
                {
                    _dxgiCapturer = DxgiScreenCapturer.TryCreate();
                    _dxgiAttempted = true;
                }
            }
        }

        return _dxgiCapturer?.CaptureFrame(out width, out height);
    }

    private static Screenshot CaptureGdiWithCursor(int x, int y, int width, int height, CaptureOptions options)
    {
        var pixelData = CapturePixelsGdi(x, y, width, height, options.IncludeCursor);
        return EncodeScreenshot(pixelData, width, height, options);
    }

    private static byte[] CapturePixelsGdi(int x, int y, int width, int height, bool includeCursor)
    {
        var screenDc = PInvoke.GetDC(HWND.Null);
        if (screenDc.IsNull)
            throw new HarnessException("Failed to get screen DC");

        try
        {
            unsafe
            {
                var memDc = PInvoke.CreateCompatibleDC(screenDc);
                if (memDc.IsNull)
                    throw new HarnessException("Failed to create compatible DC");

                try
                {
                    var hBitmap = PInvoke.CreateCompatibleBitmap(screenDc, width, height);
                    if (hBitmap.IsNull)
                        throw new HarnessException("Failed to create compatible bitmap");

                    try
                    {
                        var oldObj = PInvoke.SelectObject(memDc, hBitmap);

                        PInvoke.BitBlt(
                            memDc, 0, 0, width, height,
                            screenDc, x, y,
                            ROP_CODE.SRCCOPY);

                        // Draw cursor overlay onto the captured bitmap
                        if (includeCursor)
                            DrawCursorOnDc(memDc, x, y);

                        PInvoke.SelectObject(memDc, oldObj);

                        var bmi = new BITMAPINFO
                        {
                            bmiHeader = new BITMAPINFOHEADER
                            {
                                biSize = (uint)sizeof(BITMAPINFOHEADER),
                                biWidth = width,
                                biHeight = -height, // top-down
                                biPlanes = 1,
                                biBitCount = 32,
                                biCompression = (int)BI_COMPRESSION.BI_RGB,
                            }
                        };

                        var pixelData = new byte[width * height * 4];
                        fixed (byte* pPixels = pixelData)
                        {
                            var result = PInvoke.GetDIBits(
                                memDc, hBitmap, 0, (uint)height,
                                pPixels, &bmi, DIB_USAGE.DIB_RGB_COLORS);

                            if (result == 0)
                                throw new HarnessException("GetDIBits failed");
                        }

                        return pixelData;
                    }
                    finally
                    {
                        PInvoke.DeleteObject(hBitmap);
                    }
                }
                finally
                {
                    PInvoke.DeleteDC(memDc);
                }
            }
        }
        finally
        {
            _ = PInvoke.ReleaseDC(HWND.Null, screenDc);
        }
    }

    /// <summary>
    /// Draws the current cursor onto a device context at its screen position,
    /// adjusted for the capture region offset.
    /// </summary>
    private static unsafe void DrawCursorOnDc(HDC hdc, int regionX, int regionY)
    {
        var ci = new CURSORINFO { cbSize = (uint)sizeof(CURSORINFO) };
        if (!PInvoke.GetCursorInfo(ref ci))
            return;

        // Only draw if cursor is showing
        if ((ci.flags & CURSORINFO_FLAGS.CURSOR_SHOWING) == 0)
            return;

        var hIcon = PInvoke.CopyIcon(new HICON((nint)ci.hCursor));
        if (hIcon.IsNull)
            return;

        try
        {
            // Get icon hotspot to draw at correct position
            ICONINFO iconInfo;
            if (!PInvoke.GetIconInfo(hIcon, &iconInfo))
            {
                // Fallback: draw without hotspot adjustment
                PInvoke.DrawIconEx(hdc,
                    ci.ptScreenPos.X - regionX,
                    ci.ptScreenPos.Y - regionY,
                    hIcon, 0, 0, 0, HBRUSH.Null, DI_FLAGS.DI_NORMAL);
                return;
            }

            // Clean up ICONINFO bitmaps
            if (!iconInfo.hbmMask.IsNull) PInvoke.DeleteObject(iconInfo.hbmMask);
            if (!iconInfo.hbmColor.IsNull) PInvoke.DeleteObject(iconInfo.hbmColor);

            // Draw cursor at its screen position minus the capture region offset
            PInvoke.DrawIconEx(hdc,
                ci.ptScreenPos.X - regionX - (int)iconInfo.xHotspot,
                ci.ptScreenPos.Y - regionY - (int)iconInfo.yHotspot,
                hIcon, 0, 0, 0, HBRUSH.Null, DI_FLAGS.DI_NORMAL);
        }
        finally
        {
            PInvoke.DestroyIcon(hIcon);
        }
    }

    /// <summary>
    /// Composites cursor onto raw BGRA pixel data (for DXGI capture path).
    /// Falls back to a simple approach: capture cursor info and draw via temporary GDI DC.
    /// </summary>
    private static unsafe void CompositeCursor(byte[] pixelData, int width, int height, int regionX, int regionY)
    {
        var ci = new CURSORINFO { cbSize = (uint)sizeof(CURSORINFO) };
        if (!PInvoke.GetCursorInfo(ref ci))
            return;

        if ((ci.flags & CURSORINFO_FLAGS.CURSOR_SHOWING) == 0)
            return;

        var hIcon = PInvoke.CopyIcon(new HICON((nint)ci.hCursor));
        if (hIcon.IsNull)
            return;

        var screenDc = PInvoke.GetDC(HWND.Null);
        try
        {
            var memDc = PInvoke.CreateCompatibleDC(screenDc);
            try
            {
                var hBitmap = PInvoke.CreateCompatibleBitmap(screenDc, width, height);
                try
                {
                    var oldObj = PInvoke.SelectObject(memDc, hBitmap);

                    // Copy pixel data into the bitmap
                    var bmi = new BITMAPINFO
                    {
                        bmiHeader = new BITMAPINFOHEADER
                        {
                            biSize = (uint)sizeof(BITMAPINFOHEADER),
                            biWidth = width,
                            biHeight = -height,
                            biPlanes = 1,
                            biBitCount = 32,
                            biCompression = (int)BI_COMPRESSION.BI_RGB,
                        }
                    };

                    fixed (byte* pPixels = pixelData)
                    {
                        _ = PInvoke.SetDIBitsToDevice(memDc, 0, 0, (uint)width, (uint)height,
                            0, 0, 0, (uint)height, pPixels, &bmi, DIB_USAGE.DIB_RGB_COLORS);
                    }

                    // Draw cursor
                    DrawCursorOnDc(memDc, regionX, regionY);

                    // Read back pixel data
                    fixed (byte* pPixels = pixelData)
                    {
                        _ = PInvoke.GetDIBits(memDc, hBitmap, 0, (uint)height,
                            pPixels, &bmi, DIB_USAGE.DIB_RGB_COLORS);
                    }

                    PInvoke.SelectObject(memDc, oldObj);
                }
                finally
                {
                    PInvoke.DeleteObject(hBitmap);
                }
            }
            finally
            {
                PInvoke.DeleteDC(memDc);
            }
        }
        finally
        {
            _ = PInvoke.ReleaseDC(HWND.Null, screenDc);
            PInvoke.DestroyIcon(hIcon);
        }
    }

    private static Screenshot EncodeScreenshot(byte[] pixelData, int width, int height, CaptureOptions options)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

        var destPtr = bitmap.GetPixels();
        Marshal.Copy(pixelData, 0, destPtr, pixelData.Length);

        var targetW = options.TargetWidth ?? width;
        var targetH = options.TargetHeight ?? height;
        var needsResize = targetW != width || targetH != height;

        SKBitmap? resized = null;
        try
        {
            var source = bitmap;
            if (needsResize)
            {
                resized = bitmap.Resize(new SKSizeI(targetW, targetH), SKSamplingOptions.Default);
                if (resized is null)
                    throw new HarnessException($"Failed to resize screenshot to {targetW}x{targetH}");
                source = resized;
            }

            var skFormat = options.Format == ImageFormat.Jpeg
                ? SKEncodedImageFormat.Jpeg
                : SKEncodedImageFormat.Png;
            var quality = options.Format == ImageFormat.Jpeg ? options.Quality : 100;

            using var image = SKImage.FromBitmap(source);
            using var data = image.Encode(skFormat, quality);

            return new Screenshot
            {
                Bytes = data.ToArray(),
                MimeType = options.Format == ImageFormat.Jpeg ? "image/jpeg" : "image/png",
                Width = source.Width,
                Height = source.Height,
                Timestamp = DateTimeOffset.UtcNow,
            };
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static readonly WindowsWindow s_windowApi = new();

    private static async Task<HWND> FindWindowHandle(string titleOrHandle, CancellationToken ct)
    {
        if (nint.TryParse(titleOrHandle, out var handleValue))
            return new HWND(handleValue);

        var windows = await s_windowApi.ListAsync(ct);
        var match = windows.FirstOrDefault(w =>
            w.Title.Contains(titleOrHandle, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            throw new HarnessException($"Window not found: {titleOrHandle}");

        return new HWND(match.Handle);
    }

    private static readonly WindowsDisplay s_displayApi = new();

    public async Task<Screenshot> CaptureMonitorAsync(int monitorIndex, CaptureOptions? options = null, CancellationToken ct = default)
    {
        options ??= new CaptureOptions();

        var monitors = await s_displayApi.GetMonitorsAsync(ct);

        if (monitorIndex < 0 || monitorIndex >= monitors.Count)
            throw new HarnessException($"Monitor index {monitorIndex} out of range (0-{monitors.Count - 1})");

        var monitor = monitors[monitorIndex];
        var bounds = monitor.Bounds;

        return await Task.Run(() =>
            CaptureGdiWithCursor(bounds.X, bounds.Y, bounds.Width, bounds.Height, options), ct);
    }

    public void Dispose()
    {
        _dxgiCapturer?.Dispose();
        _dxgiCapturer = null;
    }
}
