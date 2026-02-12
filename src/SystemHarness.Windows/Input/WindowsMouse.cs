using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IMouse"/> using SendInput API.
/// Uses virtual desktop coordinates for multi-monitor support and DPI awareness.
/// </summary>
public sealed class WindowsMouse : IMouse
{
    public WindowsMouse()
    {
        DpiInitializer.EnsureDpiAwareness();
    }

    public Task ClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            MoveCursor(x, y);
            var (down, up) = GetButtonFlags(button);
            SendMouseEvent(down);
            SendMouseEvent(up);
        }, ct);
    }

    public Task DoubleClickAsync(int x, int y, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            MoveCursor(x, y);
            SendMouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN);
            SendMouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP);
            SendMouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN);
            SendMouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP);
        }, ct);
    }

    public Task RightClickAsync(int x, int y, CancellationToken ct = default)
    {
        return ClickAsync(x, y, MouseButton.Right, ct);
    }

    public Task DragAsync(int fromX, int fromY, int toX, int toY, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            MoveCursor(fromX, fromY);
            await Task.Delay(50, ct);
            SendMouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN);
            await Task.Delay(50, ct);
            MoveCursor(toX, toY);
            await Task.Delay(50, ct);
            SendMouseEvent(MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP);
        }, ct);
    }

    public Task ScrollAsync(int x, int y, int delta, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            MoveCursor(x, y);
            SendMouseWheel(delta);
        }, ct);
    }

    public Task MoveAsync(int x, int y, CancellationToken ct = default)
    {
        return Task.Run(() => MoveCursor(x, y), ct);
    }

    public Task<(int X, int Y)> GetPositionAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            PInvoke.GetCursorPos(out var point);
            return (point.X, point.Y);
        }, ct);
    }

    // --- Phase 9 Extensions ---

    public Task MiddleClickAsync(int x, int y, CancellationToken ct = default)
    {
        return ClickAsync(x, y, MouseButton.Middle, ct);
    }

    public Task ScrollHorizontalAsync(int x, int y, int delta, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            MoveCursor(x, y);
            // MOUSEEVENTF_HWHEEL: positive = right, negative = left
            SendMouseInput(0, 0, MOUSE_EVENT_FLAGS.MOUSEEVENTF_HWHEEL, (uint)(delta * 120));
        }, ct);
    }

    public Task ButtonDownAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            MoveCursor(x, y);
            var (down, _) = GetButtonFlags(button);
            SendMouseEvent(down);
        }, ct);
    }

    public Task ButtonUpAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            MoveCursor(x, y);
            var (_, up) = GetButtonFlags(button);
            SendMouseEvent(up);
        }, ct);
    }

    public Task SmoothMoveAsync(int x, int y, TimeSpan duration, CancellationToken ct = default)
    {
        return Task.Run(async () =>
        {
            PInvoke.GetCursorPos(out var startPoint);
            var startX = startPoint.X;
            var startY = startPoint.Y;

            var steps = Math.Max((int)(duration.TotalMilliseconds / 16), 1); // ~60fps
            var delayPerStep = duration / steps;

            for (var i = 1; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();

                var t = (double)i / steps;
                // Ease-in-out interpolation
                t = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;

                var currentX = (int)(startX + (x - startX) * t);
                var currentY = (int)(startY + (y - startY) * t);

                MoveCursor(currentX, currentY);
                await Task.Delay(delayPerStep, ct);
            }
        }, ct);
    }

    /// <summary>
    /// Moves the cursor to physical pixel coordinates using virtual desktop mapping.
    /// Supports multi-monitor setups including negative-origin arrangements.
    /// </summary>
    private static void MoveCursor(int x, int y)
    {
        // Virtual desktop encompasses all monitors
        var virtualLeft = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        var virtualTop = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        var virtualWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        var virtualHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);

        // Normalize to 0-65535 range over virtual desktop (65536/size for pixel-perfect rounding)
        var absX = (int)(((x - virtualLeft) * 65536.0) / virtualWidth);
        var absY = (int)(((y - virtualTop) * 65536.0) / virtualHeight);

        SendMouseInput(absX, absY,
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE |
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE |
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK, 0);
    }

    private static void SendMouseEvent(MOUSE_EVENT_FLAGS flags)
    {
        SendMouseInput(0, 0, flags, 0);
    }

    private static void SendMouseWheel(int delta)
    {
        // Standard wheel delta is 120 per notch
        SendMouseInput(0, 0, MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL, (uint)(delta * 120));
    }

    private static void SendMouseInput(int dx, int dy, MOUSE_EVENT_FLAGS flags, uint mouseData)
    {
        var input = new INPUT
        {
            type = INPUT_TYPE.INPUT_MOUSE,
        };
        input.Anonymous.mi = new MOUSEINPUT
        {
            dx = dx,
            dy = dy,
            dwFlags = flags,
            mouseData = mouseData,
        };

        unsafe
        {
            PInvoke.SendInput(new ReadOnlySpan<INPUT>(&input, 1), sizeof(INPUT));
        }
    }

    private static (MOUSE_EVENT_FLAGS down, MOUSE_EVENT_FLAGS up) GetButtonFlags(MouseButton button) => button switch
    {
        MouseButton.Left => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP),
        MouseButton.Right => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP),
        MouseButton.Middle => (MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP),
        _ => throw new HarnessException($"Unsupported mouse button: {button}"),
    };
}
