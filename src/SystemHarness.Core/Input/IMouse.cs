namespace SystemHarness;

/// <summary>
/// Mouse input simulation — click, drag, scroll, move, and advanced mouse operations.
/// </summary>
public interface IMouse
{
    /// <summary>
    /// Clicks a mouse button at the specified screen coordinates.
    /// </summary>
    Task ClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default);

    /// <summary>
    /// Double-clicks the left mouse button at the specified screen coordinates.
    /// </summary>
    Task DoubleClickAsync(int x, int y, CancellationToken ct = default);

    /// <summary>
    /// Right-clicks at the specified screen coordinates.
    /// </summary>
    Task RightClickAsync(int x, int y, CancellationToken ct = default);

    /// <summary>
    /// Drags from one screen position to another while holding the left mouse button.
    /// </summary>
    Task DragAsync(int fromX, int fromY, int toX, int toY, CancellationToken ct = default);

    /// <summary>
    /// Scrolls the mouse wheel at the specified screen coordinates.
    /// Positive <paramref name="delta"/> scrolls up, negative scrolls down.
    /// </summary>
    Task ScrollAsync(int x, int y, int delta, CancellationToken ct = default);

    /// <summary>
    /// Moves the mouse cursor to the specified screen coordinates.
    /// </summary>
    Task MoveAsync(int x, int y, CancellationToken ct = default);

    /// <summary>
    /// Gets the current mouse cursor position in screen coordinates.
    /// </summary>
    Task<(int X, int Y)> GetPositionAsync(CancellationToken ct = default);

    // --- Phase 9 Extensions (DIM for backward compatibility) ---

    /// <summary>
    /// Performs a middle mouse button click at the specified position.
    /// </summary>
    Task MiddleClickAsync(int x, int y, CancellationToken ct = default)
        => ClickAsync(x, y, MouseButton.Middle, ct);

    /// <summary>
    /// Scrolls horizontally at the specified position.
    /// Positive delta scrolls right, negative scrolls left.
    /// </summary>
    Task ScrollHorizontalAsync(int x, int y, int delta, CancellationToken ct = default)
        => throw new NotSupportedException("ScrollHorizontalAsync is not supported by this implementation.");

    /// <summary>
    /// Presses a mouse button down at the specified position (for hold operations).
    /// Must be paired with <see cref="ButtonUpAsync"/>.
    /// </summary>
    Task ButtonDownAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
        => throw new NotSupportedException("ButtonDownAsync is not supported by this implementation.");

    /// <summary>
    /// Releases a mouse button at the specified position.
    /// </summary>
    Task ButtonUpAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
        => throw new NotSupportedException("ButtonUpAsync is not supported by this implementation.");

    /// <summary>
    /// Moves the mouse smoothly from the current position to the target over the specified duration.
    /// Uses linear interpolation for animation.
    /// </summary>
    Task SmoothMoveAsync(int x, int y, TimeSpan duration, CancellationToken ct = default)
        => throw new NotSupportedException("SmoothMoveAsync is not supported by this implementation.");

    // --- Window-relative coordinate DIMs ---

    /// <summary>
    /// Moves the mouse to a position relative to the specified window's top-left corner.
    /// </summary>
    async Task MoveToWindowAsync(IWindow window, string titleOrHandle,
        int relativeX, int relativeY, CancellationToken ct = default)
    {
        var (absX, absY) = await ResolveWindowAbsoluteAsync(window, titleOrHandle, relativeX, relativeY, ct);
        await MoveAsync(absX, absY, ct);
    }

    /// <summary>
    /// Clicks at a position relative to the specified window's top-left corner.
    /// </summary>
    async Task ClickWindowAsync(IWindow window, string titleOrHandle,
        int relativeX, int relativeY,
        MouseButton button = MouseButton.Left, CancellationToken ct = default)
    {
        var (absX, absY) = await ResolveWindowAbsoluteAsync(window, titleOrHandle, relativeX, relativeY, ct);
        await ClickAsync(absX, absY, button, ct);
    }

    /// <summary>
    /// Double-clicks at a position relative to the specified window's top-left corner.
    /// </summary>
    async Task DoubleClickWindowAsync(IWindow window, string titleOrHandle,
        int relativeX, int relativeY, CancellationToken ct = default)
    {
        var (absX, absY) = await ResolveWindowAbsoluteAsync(window, titleOrHandle, relativeX, relativeY, ct);
        await DoubleClickAsync(absX, absY, ct);
    }

    /// <summary>
    /// Right-clicks at a position relative to the specified window's top-left corner.
    /// </summary>
    async Task RightClickWindowAsync(IWindow window, string titleOrHandle,
        int relativeX, int relativeY, CancellationToken ct = default)
    {
        var (absX, absY) = await ResolveWindowAbsoluteAsync(window, titleOrHandle, relativeX, relativeY, ct);
        await RightClickAsync(absX, absY, ct);
    }

    /// <summary>
    /// Resolves a window-relative coordinate to absolute screen coordinates.
    /// Note: window position is queried at call time; if the window moves between
    /// resolution and the subsequent mouse action, coordinates may be stale.
    /// </summary>
    private static async Task<(int X, int Y)> ResolveWindowAbsoluteAsync(
        IWindow window, string titleOrHandle,
        int relativeX, int relativeY, CancellationToken ct)
    {
        var windows = await window.ListAsync(ct);
        var match = nint.TryParse(titleOrHandle, out var handleValue)
            ? windows.FirstOrDefault(w => w.Handle == handleValue)
            : windows.FirstOrDefault(w => w.Title.Contains(titleOrHandle, StringComparison.OrdinalIgnoreCase));

        if (match is null)
            throw new HarnessException($"Window not found: {titleOrHandle}");

        return (match.Bounds.X + relativeX, match.Bounds.Y + relativeY);
    }
}

/// <summary>
/// Mouse button identifiers.
/// </summary>
public enum MouseButton
{
    Left,
    Right,
    Middle
}
