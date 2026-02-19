using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IWindow"/> using Win32 APIs (CsWin32).
/// </summary>
public sealed class WindowsWindow : IWindow
{
    private const uint WM_CLOSE = 0x0010;
    private const int WS_EX_LAYERED = 0x00080000;

    public Task<IReadOnlyList<WindowInfo>> ListAsync(CancellationToken ct = default)
    {
        var windows = new List<WindowInfo>();

        unsafe
        {
            PInvoke.EnumWindows((hwnd, lParam) =>
            {
                if (!PInvoke.IsWindowVisible(hwnd))
                {
                    return true; // skip invisible
                }

                var titleLength = PInvoke.GetWindowTextLength(hwnd);
                if (titleLength == 0)
                {
                    return true; // skip untitled
                }

                var titleBuffer = new char[titleLength + 1];
                int copied;
                fixed (char* pTitle = titleBuffer)
                {
                    copied = PInvoke.GetWindowText(hwnd, pTitle, titleLength + 1);
                }

                if (copied == 0)
                {
                    return true;
                }

                var title = new string(titleBuffer, 0, copied);

                PInvoke.GetWindowRect(hwnd, out var rect);

                uint processId = 0;
                _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);

                // Get class name
                string? className = null;
                var classBuffer = new char[256];
                fixed (char* pClass = classBuffer)
                {
                    var classLen = PInvoke.GetClassName(hwnd, pClass, 256);
                    if (classLen > 0)
                        className = new string(classBuffer, 0, classLen);
                }

                // Get window state
                WindowState state;
                if (PInvoke.IsIconic(hwnd))
                    state = WindowState.Minimized;
                else if (PInvoke.IsZoomed(hwnd))
                    state = WindowState.Maximized;
                else
                    state = WindowState.Normal;

                windows.Add(new WindowInfo
                {
                    Handle = (nint)hwnd.Value,
                    Title = title,
                    ProcessId = (int)processId,
                    IsVisible = true,
                    Bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height),
                    ClassName = className,
                    State = state,
                });

                return true;
            }, 0);
        }

        return Task.FromResult<IReadOnlyList<WindowInfo>>(windows);
    }

    public async Task FocusAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.SetForegroundWindow(hwnd);
    }

    public async Task MinimizeAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_MINIMIZE);
    }

    public async Task MaximizeAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_MAXIMIZE);
    }

    public async Task ResizeAsync(string titleOrHandle, int width, int height, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.GetWindowRect(hwnd, out var rect);
        PInvoke.MoveWindow(hwnd, rect.X, rect.Y, width, height, true);
    }

    public async Task MoveAsync(string titleOrHandle, int x, int y, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.GetWindowRect(hwnd, out var rect);
        PInvoke.MoveWindow(hwnd, x, y, rect.Width, rect.Height, true);
    }

    public async Task CloseAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.PostMessage(hwnd, WM_CLOSE, 0, 0);
    }

    public async Task RestoreAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
    }

    public async Task HideAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    public async Task ShowAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOW);
    }

    public async Task SetAlwaysOnTopAsync(string titleOrHandle, bool onTop, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        var insertAfter = onTop
            ? new HWND(-1)  // HWND_TOPMOST
            : new HWND(-2); // HWND_NOTOPMOST

        PInvoke.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
    }

    public async Task SetOpacityAsync(string titleOrHandle, double opacity, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);
        var alpha = (byte)Math.Clamp(opacity * 255, 0, 255);

        // Add WS_EX_LAYERED style
        var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _ = PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | WS_EX_LAYERED);

        // Set opacity via SetLayeredWindowAttributes
        PInvoke.SetLayeredWindowAttributes(hwnd, new COLORREF(0), alpha,
            LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
    }

    public Task<WindowInfo?> GetForegroundAsync(CancellationToken ct = default)
    {
        unsafe
        {
            var hwnd = PInvoke.GetForegroundWindow();
            if (hwnd.IsNull)
                return Task.FromResult<WindowInfo?>(null);

            var titleLength = PInvoke.GetWindowTextLength(hwnd);
            if (titleLength == 0)
                return Task.FromResult<WindowInfo?>(null);

            var titleBuffer = new char[titleLength + 1];
            int copied;
            fixed (char* pTitle = titleBuffer)
            {
                copied = PInvoke.GetWindowText(hwnd, pTitle, titleLength + 1);
            }

            if (copied == 0)
                return Task.FromResult<WindowInfo?>(null);

            var title = new string(titleBuffer, 0, copied);
            PInvoke.GetWindowRect(hwnd, out var rect);

            uint processId = 0;
            _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);

            WindowState state;
            if (PInvoke.IsIconic(hwnd))
                state = WindowState.Minimized;
            else if (PInvoke.IsZoomed(hwnd))
                state = WindowState.Maximized;
            else
                state = WindowState.Normal;

            return Task.FromResult<WindowInfo?>(new WindowInfo
            {
                Handle = (nint)hwnd.Value,
                Title = title,
                ProcessId = (int)processId,
                IsVisible = PInvoke.IsWindowVisible(hwnd),
                Bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height),
                State = state,
            });
        }
    }

    public async Task<WindowState> GetStateAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var hwnd = await FindWindowHandle(titleOrHandle, ct);

        if (PInvoke.IsIconic(hwnd))
            return WindowState.Minimized;
        if (PInvoke.IsZoomed(hwnd))
            return WindowState.Maximized;
        return WindowState.Normal;
    }

    public async Task<WindowInfo> WaitForWindowAsync(string titleSubstring, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        timeout ??= TimeSpan.FromSeconds(30);
        var deadline = DateTime.UtcNow + timeout.Value;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var windows = await ListAsync(ct);
            var match = windows.FirstOrDefault(w =>
                w.Title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return match;

            await Task.Delay(250, ct);
        }

        throw new HarnessException($"Timed out waiting for window: {titleSubstring}");
    }

    public Task<IReadOnlyList<WindowInfo>> FindByProcessIdAsync(int pid, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var allWindows = await ListAsync(ct);
            var result = allWindows.Where(w => w.ProcessId == pid).ToList();
            return (IReadOnlyList<WindowInfo>)result;
        }, ct);
    }

    public Task<IReadOnlyList<WindowInfo>> GetChildWindowsAsync(string titleOrHandle, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            var parentHwnd = await FindWindowHandle(titleOrHandle, ct);
            var children = new List<WindowInfo>();

            unsafe
            {
                PInvoke.EnumChildWindows(parentHwnd, (hwnd, lParam) =>
                {
                    var titleLength = PInvoke.GetWindowTextLength(hwnd);
                    var title = string.Empty;
                    if (titleLength > 0)
                    {
                        var titleBuffer = new char[titleLength + 1];
                        int copied;
                        fixed (char* pTitle = titleBuffer)
                        {
                            copied = PInvoke.GetWindowText(hwnd, pTitle, titleLength + 1);
                        }
                        if (copied > 0)
                            title = new string(titleBuffer, 0, copied);
                    }

                    PInvoke.GetWindowRect(hwnd, out var rect);

                    uint processId = 0;
                    _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);

                    // Get class name
                    string? className = null;
                    var classBuffer = new char[256];
                    fixed (char* pClass = classBuffer)
                    {
                        var classLen = PInvoke.GetClassName(hwnd, pClass, 256);
                        if (classLen > 0)
                            className = new string(classBuffer, 0, classLen);
                    }

                    children.Add(new WindowInfo
                    {
                        Handle = (nint)hwnd.Value,
                        Title = title,
                        ProcessId = (int)processId,
                        IsVisible = PInvoke.IsWindowVisible(hwnd),
                        Bounds = new Rectangle(rect.X, rect.Y, rect.Width, rect.Height),
                        ClassName = className,
                        ParentHandle = (nint)parentHwnd.Value,
                    });

                    return true;
                }, 0);
            }

            return (IReadOnlyList<WindowInfo>)children;
        }, ct);
    }

    internal async Task<HWND> FindWindowHandle(string titleOrHandle, CancellationToken ct)
    {
        // Try parsing as a handle first
        if (nint.TryParse(titleOrHandle, out var handleValue))
        {
            return new HWND(handleValue);
        }

        // Search by title substring
        var windows = await ListAsync(ct);
        var match = windows.FirstOrDefault(w =>
            w.Title.Contains(titleOrHandle, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new HarnessException($"Window not found: {titleOrHandle}");
        }

        return new HWND(match.Handle);
    }

}
