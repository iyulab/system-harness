using System.Runtime.InteropServices;
using Windows.Win32;

namespace SystemHarness.Windows;

/// <summary>
/// Ensures the process is Per-Monitor DPI V2 aware.
/// Must be called before any HWND creation for full effect.
/// </summary>
internal static class DpiInitializer
{
    private static readonly object Lock = new();
    private static volatile bool _initialized;

    /// <summary>
    /// Sets the process DPI awareness to Per-Monitor V2.
    /// Safe to call multiple times — only the first call takes effect.
    /// </summary>
    public static void EnsureDpiAwareness()
    {
        if (_initialized) return;

        lock (Lock)
        {
            if (_initialized) return;

            // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = ((DPI_AWARENESS_CONTEXT)-4)
            if (!SetProcessDpiAwarenessContext(-4))
            {
                // Fallback: may already be set by manifest, or running on older Windows
                // This is non-fatal — coordinate calculations may be incorrect on high-DPI
                System.Diagnostics.Debug.WriteLine(
                    $"SetProcessDpiAwarenessContext failed (error {Marshal.GetLastWin32Error()}). " +
                    "DPI awareness may be set via manifest.");
            }

            _initialized = true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(nint value);
}
