namespace SystemHarness;

/// <summary>
/// Pure coordinate transformation utilities for window-relative and screen-absolute conversions.
/// All methods are static and require no <see cref="IHarness"/> instance.
/// </summary>
public static class CoordinateHelpers
{
    /// <summary>
    /// Converts window-relative coordinates to absolute screen coordinates.
    /// </summary>
    public static (int X, int Y) WindowToScreen(WindowInfo window, int relativeX, int relativeY)
        => (window.Bounds.X + relativeX, window.Bounds.Y + relativeY);

    /// <summary>
    /// Converts absolute screen coordinates to window-relative coordinates.
    /// </summary>
    public static (int X, int Y) ScreenToWindow(WindowInfo window, int screenX, int screenY)
        => (screenX - window.Bounds.X, screenY - window.Bounds.Y);

    /// <summary>
    /// Returns the center point of a rectangle.
    /// </summary>
    public static (int X, int Y) Center(Rectangle rect)
        => (rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    /// <summary>
    /// Returns the center point of an OCR word's bounding rectangle (ideal click target).
    /// </summary>
    public static (int X, int Y) Center(OcrWord word)
        => Center(word.BoundingRect);

    /// <summary>
    /// Returns the center point of an OCR line's bounding rectangle.
    /// </summary>
    public static (int X, int Y) Center(OcrLine line)
        => Center(line.BoundingRect);

    /// <summary>
    /// Returns the center point of a UI element's bounding rectangle.
    /// </summary>
    public static (int X, int Y) Center(UIElement element)
        => Center(element.BoundingRectangle);
}
