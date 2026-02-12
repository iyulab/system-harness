using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IDisplay"/> using Win32 monitor APIs.
/// </summary>
public sealed class WindowsDisplay : IDisplay
{
    public WindowsDisplay()
    {
        DpiInitializer.EnsureDpiAwareness();
    }

    public Task<IReadOnlyList<MonitorInfo>> GetMonitorsAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var handles = new List<HMONITOR>();

            unsafe
            {
                PInvoke.EnumDisplayMonitors(HDC.Null, (RECT*)null, (hMonitor, hdc, lpRect, data) =>
                {
                    handles.Add(hMonitor);
                    return true;
                }, 0);
            }

            var monitors = new List<MonitorInfo>();
            for (var i = 0; i < handles.Count; i++)
            {
                var info = GetMonitorInfoFromHandle(handles[i], i);
                if (info is not null)
                    monitors.Add(info);
            }

            return (IReadOnlyList<MonitorInfo>)monitors;
        }, ct);
    }

    public async Task<MonitorInfo> GetPrimaryMonitorAsync(CancellationToken ct = default)
    {
        var monitors = await GetMonitorsAsync(ct);
        return monitors.FirstOrDefault(m => m.IsPrimary)
            ?? monitors.First();
    }

    public async Task<MonitorInfo> GetMonitorAtPointAsync(int x, int y, CancellationToken ct = default)
    {
        var monitors = await GetMonitorsAsync(ct);
        var hMonitor = PInvoke.MonitorFromPoint(
            new System.Drawing.Point(x, y),
            MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        return FindMonitorByHandle(monitors, hMonitor);
    }

    public async Task<MonitorInfo> GetMonitorForWindowAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await WindowHandleResolver.ResolveAsync(titleOrHandle, ct);

        var monitors = await GetMonitorsAsync(ct);
        var hMonitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        return FindMonitorByHandle(monitors, hMonitor);
    }

    public Task<Rectangle> GetVirtualScreenBoundsAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var x = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
            var y = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
            var width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
            var height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);

            return new Rectangle(x, y, width, height);
        }, ct);
    }

    private static unsafe MonitorInfo? GetMonitorInfoFromHandle(HMONITOR hMonitor, int index)
    {
        var monitorInfo = new MONITORINFOEXW();
        monitorInfo.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);

        if (!PInvoke.GetMonitorInfo(hMonitor, (MONITORINFO*)&monitorInfo))
            return null;

        var mi = monitorInfo.monitorInfo;
        var isPrimary = (mi.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY

        // Get DPI
        double dpiX = 96, dpiY = 96;
        try
        {
            uint rawDpiX = 0, rawDpiY = 0;
            PInvoke.GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
                &rawDpiX, &rawDpiY);
            dpiX = rawDpiX;
            dpiY = rawDpiY;
        }
        catch (EntryPointNotFoundException)
        {
            // GetDpiForMonitor not available on Windows 7 and earlier — use 96 default
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetDpiForMonitor failed for monitor {index}: {ex.Message}");
        }

        var deviceName = new string(monitorInfo.szDevice.AsSpan()).TrimEnd('\0');

        return new MonitorInfo
        {
            Index = index,
            Name = deviceName,
            Bounds = new Rectangle(
                mi.rcMonitor.X, mi.rcMonitor.Y,
                mi.rcMonitor.Width, mi.rcMonitor.Height),
            WorkArea = new Rectangle(
                mi.rcWork.X, mi.rcWork.Y,
                mi.rcWork.Width, mi.rcWork.Height),
            IsPrimary = isPrimary,
            DpiX = dpiX,
            DpiY = dpiY,
            ScaleFactor = dpiX / 96.0,
            Handle = (nint)hMonitor.Value,
        };
    }

    private static unsafe MonitorInfo FindMonitorByHandle(IReadOnlyList<MonitorInfo> monitors, HMONITOR hMonitor)
    {
        var handle = (nint)hMonitor.Value;
        return monitors.FirstOrDefault(m => m.Handle == handle)
            ?? monitors.First();
    }
}
