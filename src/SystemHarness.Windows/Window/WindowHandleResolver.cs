using Windows.Win32;
using Windows.Win32.Foundation;

namespace SystemHarness.Windows;

/// <summary>
/// Resolves a title substring or handle string to an HWND without allocating a WindowsWindow instance.
/// </summary>
internal static class WindowHandleResolver
{
    internal static Task<HWND> ResolveAsync(string titleOrHandle, CancellationToken ct)
    {
        // Try parsing as a handle first
        if (nint.TryParse(titleOrHandle, out var handleValue))
        {
            return Task.FromResult(new HWND(handleValue));
        }

        // Search by title substring via EnumWindows
        HWND found = default;
        unsafe
        {
            PInvoke.EnumWindows((hwnd, _) =>
            {
                if (!PInvoke.IsWindowVisible(hwnd))
                    return true;

                var titleLength = PInvoke.GetWindowTextLength(hwnd);
                if (titleLength == 0)
                    return true;

                Span<char> buffer = stackalloc char[titleLength + 1];
                int copied;
                fixed (char* pTitle = buffer)
                {
                    copied = PInvoke.GetWindowText(hwnd, pTitle, titleLength + 1);
                }

                if (copied > 0)
                {
                    var title = buffer[..copied];
                    if (title.Contains(titleOrHandle, StringComparison.OrdinalIgnoreCase))
                    {
                        found = hwnd;
                        return false; // stop enumeration
                    }
                }

                return true;
            }, 0);
        }

        if (found.IsNull)
            throw new HarnessException($"Window not found: {titleOrHandle}");

        return Task.FromResult(found);
    }
}
