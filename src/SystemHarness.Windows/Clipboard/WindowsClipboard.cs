using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IClipboard"/> using Win32 APIs (CsWin32).
/// </summary>
public sealed class WindowsClipboard : IClipboard
{
    private const uint CF_UNICODETEXT = 13;
    private const uint CF_DIB = 8;
    private const int BmpFileHeaderSize = 14;
    private const int MaxRetries = 10;
    private const int RetryDelayMs = 50;

    private static bool TryOpenClipboard()
    {
        for (var i = 0; i < MaxRetries; i++)
        {
            if (PInvoke.OpenClipboard(HWND.Null))
                return true;
            Thread.Sleep(RetryDelayMs);
        }
        return false;
    }

    public Task<string?> GetTextAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!TryOpenClipboard())
                return null;

            try
            {
                if (!PInvoke.IsClipboardFormatAvailable(CF_UNICODETEXT))
                    return null;

                unsafe
                {
                    var hData = PInvoke.GetClipboardData(CF_UNICODETEXT);
                    if (hData.Value == null)
                        return null;

                    var hGlobal = new HGLOBAL((nint)hData.Value);
                    var ptr = PInvoke.GlobalLock(hGlobal);
                    if (ptr == null)
                        return null;

                    try
                    {
                        return Marshal.PtrToStringUni((nint)ptr);
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(hGlobal);
                    }
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }, ct);
    }

    public Task SetTextAsync(string text, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!TryOpenClipboard())
                throw new HarnessException("Failed to open clipboard");

            try
            {
                PInvoke.EmptyClipboard();

                var byteCount = (nuint)((text.Length + 1) * sizeof(char));
                var hGlobal = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, byteCount);

                unsafe
                {
                    if (hGlobal.Value == null)
                        throw new HarnessException("Failed to allocate global memory");

                    var ptr = PInvoke.GlobalLock(hGlobal);
                    if (ptr == null)
                    {
                        PInvoke.GlobalFree(hGlobal);
                        throw new HarnessException("Failed to lock global memory");
                    }

                    try
                    {
                        fixed (char* pText = text)
                        {
                            Buffer.MemoryCopy(pText, ptr, (long)byteCount, text.Length * sizeof(char));
                            ((char*)ptr)[text.Length] = '\0';
                        }
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(hGlobal);
                    }

                    var handle = new HANDLE(hGlobal.Value);
                    if (PInvoke.SetClipboardData(CF_UNICODETEXT, handle).Value == null)
                    {
                        PInvoke.GlobalFree(hGlobal);
                        throw new HarnessException("Failed to set clipboard data");
                    }
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }, ct);
    }

    public Task<byte[]?> GetImageAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            if (!TryOpenClipboard())
                return (byte[]?)null;

            try
            {
                if (!PInvoke.IsClipboardFormatAvailable(CF_DIB))
                    return null;

                unsafe
                {
                    var hData = PInvoke.GetClipboardData(CF_DIB);
                    if (hData.Value == null)
                        return null;

                    var hGlobal = new HGLOBAL((nint)hData.Value);
                    var size = (int)PInvoke.GlobalSize(hGlobal);
                    if (size == 0)
                        return null;

                    var ptr = PInvoke.GlobalLock(hGlobal);
                    if (ptr == null)
                        return null;

                    try
                    {
                        // Read BITMAPINFOHEADER to calculate pixel data offset
                        var headerSize = Marshal.ReadInt32((nint)ptr); // biSize
                        var bitsPerPixel = Marshal.ReadInt16((nint)ptr + 14); // biBitCount
                        var colorsUsed = Marshal.ReadInt32((nint)ptr + 32); // biClrUsed

                        // Calculate color table size
                        var colorTableEntries = colorsUsed > 0
                            ? colorsUsed
                            : bitsPerPixel <= 8 ? (1 << bitsPerPixel) : 0;
                        var colorTableSize = colorTableEntries * 4; // RGBQUAD = 4 bytes

                        var pixelDataOffset = headerSize + colorTableSize;

                        // Build BMP: file header (14 bytes) + DIB data
                        var bmp = new byte[BmpFileHeaderSize + size];
                        bmp[0] = (byte)'B';
                        bmp[1] = (byte)'M';
                        BitConverter.TryWriteBytes(bmp.AsSpan(2), bmp.Length); // bfSize
                        // bfReserved1, bfReserved2 = 0 (already zeroed)
                        BitConverter.TryWriteBytes(bmp.AsSpan(10), BmpFileHeaderSize + pixelDataOffset); // bfOffBits

                        new Span<byte>(ptr, size).CopyTo(bmp.AsSpan(BmpFileHeaderSize));
                        return bmp;
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(hGlobal);
                    }
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }, ct);
    }

    // --- Phase 9 Extensions ---

    public Task<string?> GetHtmlAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var cfHtml = PInvoke.RegisterClipboardFormat("HTML Format");
            if (cfHtml == 0) return (string?)null;

            if (!TryOpenClipboard())
                return null;

            try
            {
                if (!PInvoke.IsClipboardFormatAvailable(cfHtml))
                    return null;

                unsafe
                {
                    var hData = PInvoke.GetClipboardData(cfHtml);
                    if (hData.Value == null) return null;

                    var hGlobal = new HGLOBAL((nint)hData.Value);
                    var ptr = PInvoke.GlobalLock(hGlobal);
                    if (ptr == null) return null;

                    try
                    {
                        var size = (int)PInvoke.GlobalSize(hGlobal);
                        var rawBytes = new ReadOnlySpan<byte>(ptr, size);

                        // Parse header byte offsets from the raw UTF-8 bytes (header is always ASCII)
                        // to avoid decoding the entire buffer just to read header values
                        if (TryParseByteOffset(rawBytes, "StartFragment:"u8, out var start) &&
                            TryParseByteOffset(rawBytes, "EndFragment:"u8, out var end) &&
                            start >= 0 && end > start && end <= size)
                        {
                            return System.Text.Encoding.UTF8.GetString(rawBytes.Slice(start, end - start));
                        }

                        // Fallback: decode entire content
                        return System.Text.Encoding.UTF8.GetString(rawBytes);
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(hGlobal);
                    }
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }, ct);
    }

    public Task SetHtmlAsync(string html, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var cfHtml = PInvoke.RegisterClipboardFormat("HTML Format");
            if (cfHtml == 0)
                throw new HarnessException("Failed to register HTML clipboard format");

            // Build CF_HTML header
            var header = BuildHtmlClipboardFormat(html);
            var bytes = System.Text.Encoding.UTF8.GetBytes(header);

            if (!TryOpenClipboard())
                throw new HarnessException("Failed to open clipboard");

            try
            {
                PInvoke.EmptyClipboard();
                var hGlobal = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)(bytes.Length + 1));

                unsafe
                {
                    if (hGlobal.Value == null)
                        throw new HarnessException("Failed to allocate global memory");

                    var ptr = PInvoke.GlobalLock(hGlobal);
                    if (ptr == null)
                    {
                        PInvoke.GlobalFree(hGlobal);
                        throw new HarnessException("Failed to lock global memory");
                    }

                    try
                    {
                        Marshal.Copy(bytes, 0, (nint)ptr, bytes.Length);
                        ((byte*)ptr)[bytes.Length] = 0; // null terminator
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(hGlobal);
                    }

                    var handle = new HANDLE(hGlobal.Value);
                    if (PInvoke.SetClipboardData(cfHtml, handle).Value == null)
                    {
                        PInvoke.GlobalFree(hGlobal);
                        throw new HarnessException("Failed to set clipboard data");
                    }
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }, ct);
    }

    public Task<IReadOnlyList<string>?> GetFileDropListAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            const uint CF_HDROP = 15;

            if (!TryOpenClipboard())
                return (IReadOnlyList<string>?)null;

            try
            {
                if (!PInvoke.IsClipboardFormatAvailable(CF_HDROP))
                    return null;

                unsafe
                {
                    var hData = PInvoke.GetClipboardData(CF_HDROP);
                    if (hData.Value == null) return null;

                    var hDrop = (nint)hData.Value;
                    var count = DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
                    if (count == 0) return null;

                    var files = new List<string>((int)count);
                    for (uint i = 0; i < count; i++)
                    {
                        var len = DragQueryFileW(hDrop, i, null, 0);
                        if (len == 0) continue;

                        var buffer = new char[len + 1];
                        fixed (char* pBuf = buffer)
                        {
                            _ = DragQueryFileW(hDrop, i, pBuf, len + 1);
                        }
                        files.Add(new string(buffer, 0, (int)len));
                    }

                    return (IReadOnlyList<string>)files;
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }, ct);
    }

    public Task SetFileDropListAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
            throw new ArgumentException("At least one path must be provided.", nameof(paths));

        return Task.Run(() =>
        {
            const uint CF_HDROP = 15;

            if (!TryOpenClipboard())
                throw new HarnessException("Failed to open clipboard");

            try
            {
                PInvoke.EmptyClipboard();

                // Build DROPFILES structure:
                // DROPFILES header (20 bytes) + null-separated file paths + double null
                const int dropFilesSize = 20; // sizeof(DROPFILES)

                // Calculate total char count: sum of path lengths + null per path + trailing null
                var charCount = 0;
                foreach (var path in paths)
                    charCount += path.Length + 1; // path + null separator
                charCount += 1; // double null terminator

                var totalSize = dropFilesSize + charCount * sizeof(char);

                var hGlobal = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT,
                    (nuint)totalSize);

                unsafe
                {
                    if (hGlobal.Value == null)
                        throw new HarnessException("Failed to allocate global memory");

                    var ptr = (byte*)PInvoke.GlobalLock(hGlobal);
                    if (ptr == null)
                    {
                        PInvoke.GlobalFree(hGlobal);
                        throw new HarnessException("Failed to lock global memory");
                    }

                    try
                    {
                        // DROPFILES structure
                        *(int*)(ptr + 0) = dropFilesSize; // pFiles offset
                        *(int*)(ptr + 4) = 0;  // pt.x
                        *(int*)(ptr + 8) = 0;  // pt.y
                        *(int*)(ptr + 12) = 0; // fNC
                        *(int*)(ptr + 16) = 1; // fWide = TRUE (Unicode)

                        // Write paths directly into the buffer
                        var dest = (char*)(ptr + dropFilesSize);
                        var offset = 0;
                        foreach (var path in paths)
                        {
                            path.CopyTo(new Span<char>(dest + offset, path.Length));
                            offset += path.Length;
                            dest[offset++] = '\0';
                        }
                        dest[offset] = '\0'; // double null terminator
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(hGlobal);
                    }

                    var handle = new HANDLE(hGlobal.Value);
                    if (PInvoke.SetClipboardData(CF_HDROP, handle).Value == null)
                    {
                        PInvoke.GlobalFree(hGlobal);
                        throw new HarnessException("Failed to set clipboard data");
                    }
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }, ct);
    }

    public Task<IReadOnlyList<string>> GetAvailableFormatsAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var formats = new List<string>();

            if (!TryOpenClipboard())
                return (IReadOnlyList<string>)formats;

            try
            {
                uint format = 0;
                while ((format = PInvoke.EnumClipboardFormats(format)) != 0)
                {
                    var name = GetClipboardFormatName(format);
                    formats.Add(name ?? $"CF_{format}");
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }

            return (IReadOnlyList<string>)formats;
        }, ct);
    }

    private static string BuildHtmlClipboardFormat(string htmlFragment)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string startHtml = "<html><body>\r\n<!--StartFragment-->";
        const string endHtml = "<!--EndFragment-->\r\n</body></html>";

        var headerLen = string.Format(CultureInfo.InvariantCulture, header, 0, 0, 0, 0).Length;
        var startHtmlIdx = headerLen;
        var startFragIdx = startHtmlIdx + startHtml.Length;
        var endFragIdx = startFragIdx + System.Text.Encoding.UTF8.GetByteCount(htmlFragment);
        var endHtmlIdx = endFragIdx + endHtml.Length;

        return string.Format(CultureInfo.InvariantCulture, header, startHtmlIdx, endHtmlIdx, startFragIdx, endFragIdx)
            + startHtml + htmlFragment + endHtml;
    }

    private static string? GetClipboardFormatName(uint format)
    {
        // Standard format names
        return format switch
        {
            1 => "CF_TEXT",
            2 => "CF_BITMAP",
            3 => "CF_METAFILEPICT",
            4 => "CF_SYLK",
            5 => "CF_DIF",
            6 => "CF_TIFF",
            7 => "CF_OEMTEXT",
            CF_DIB => "CF_DIB",
            9 => "CF_PALETTE",
            10 => "CF_PENDATA",
            11 => "CF_RIFF",
            12 => "CF_WAVE",
            CF_UNICODETEXT => "CF_UNICODETEXT",
            14 => "CF_ENHMETAFILE",
            15 => "CF_HDROP",
            16 => "CF_LOCALE",
            17 => "CF_DIBV5",
            _ => GetRegisteredFormatName(format),
        };
    }

    private static unsafe string? GetRegisteredFormatName(uint format)
    {
        var buffer = new char[256];
        fixed (char* pBuf = buffer)
        {
            var len = PInvoke.GetClipboardFormatName(format, pBuf, 256);
            return len > 0 ? new string(buffer, 0, len) : null;
        }
    }

    /// <summary>
    /// Parses a byte offset value from CF_HTML raw bytes by searching for a header label
    /// (e.g., "StartFragment:") and parsing the integer value that follows it.
    /// Works directly on UTF-8 bytes without decoding the entire buffer.
    /// </summary>
    private static bool TryParseByteOffset(ReadOnlySpan<byte> data, ReadOnlySpan<byte> label, out int value)
    {
        value = 0;
        var idx = data.IndexOf(label);
        if (idx < 0) return false;

        var offset = idx + label.Length;
        if (offset >= data.Length) return false;

        var remaining = data[offset..];
        var endIdx = remaining.IndexOfAny((byte)'\r', (byte)'\n');
        if (endIdx <= 0) return false;

        // Parse ASCII digits directly from bytes
        var numSpan = remaining[..endIdx];
        // Trim leading/trailing spaces
        while (numSpan.Length > 0 && numSpan[0] == ' ') numSpan = numSpan[1..];
        while (numSpan.Length > 0 && numSpan[^1] == ' ') numSpan = numSpan[..^1];

        if (numSpan.Length == 0) return false;

        var result = 0;
        foreach (var b in numSpan)
        {
            if (b < '0' || b > '9') return false;
            result = result * 10 + (b - '0');
        }
        value = result;
        return true;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern unsafe uint DragQueryFileW(nint hDrop, uint iFile, char* lpszFile, uint cch);

    public Task SetImageAsync(byte[] imageData, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            // Expect BMP format: strip 14-byte file header to get DIB
            if (imageData.Length < BmpFileHeaderSize + 40 ||
                imageData[0] != (byte)'B' || imageData[1] != (byte)'M')
            {
                throw new HarnessException("Image data must be in BMP format");
            }

            var dibData = imageData.AsSpan(BmpFileHeaderSize);

            if (!TryOpenClipboard())
                throw new HarnessException("Failed to open clipboard");

            try
            {
                PInvoke.EmptyClipboard();

                var hGlobal = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)dibData.Length);

                unsafe
                {
                    if (hGlobal.Value == null)
                        throw new HarnessException("Failed to allocate global memory");

                    var ptr = PInvoke.GlobalLock(hGlobal);
                    if (ptr == null)
                    {
                        PInvoke.GlobalFree(hGlobal);
                        throw new HarnessException("Failed to lock global memory");
                    }

                    try
                    {
                        dibData.CopyTo(new Span<byte>(ptr, dibData.Length));
                    }
                    finally
                    {
                        PInvoke.GlobalUnlock(hGlobal);
                    }

                    var handle = new HANDLE(hGlobal.Value);
                    if (PInvoke.SetClipboardData(CF_DIB, handle).Value == null)
                    {
                        PInvoke.GlobalFree(hGlobal);
                        throw new HarnessException("Failed to set clipboard data");
                    }
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }, ct);
    }
}
