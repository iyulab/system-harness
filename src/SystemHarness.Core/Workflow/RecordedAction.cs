namespace SystemHarness;

/// <summary>
/// A single recorded user action (mouse or keyboard event).
/// </summary>
public sealed class RecordedAction
{
    /// <summary>
    /// The type of action recorded.
    /// </summary>
    public required RecordedActionType Type { get; init; }

    /// <summary>
    /// Timestamp when the action occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Screen X coordinate (for mouse actions).
    /// </summary>
    public int? X { get; init; }

    /// <summary>
    /// Screen Y coordinate (for mouse actions).
    /// </summary>
    public int? Y { get; init; }

    /// <summary>
    /// Mouse button (for click actions).
    /// </summary>
    public MouseButton? Button { get; init; }

    /// <summary>
    /// Key (for keyboard actions).
    /// </summary>
    public Key? Key { get; init; }

    /// <summary>
    /// Scroll delta (for scroll actions). Positive = up, negative = down.
    /// </summary>
    public int? ScrollDelta { get; init; }

    /// <summary>
    /// Delay before this action relative to the previous action.
    /// Used during replay to preserve original timing.
    /// </summary>
    public TimeSpan DelayBefore { get; init; }
}

/// <summary>
/// Types of recorded user actions.
/// </summary>
public enum RecordedActionType
{
    /// <summary>Mouse cursor movement.</summary>
    MouseMove,
    /// <summary>Mouse button click (down + up).</summary>
    MouseClick,
    /// <summary>Mouse button press (down only).</summary>
    MouseDown,
    /// <summary>Mouse button release (up only).</summary>
    MouseUp,
    /// <summary>Mouse scroll wheel.</summary>
    MouseScroll,
    /// <summary>Key press (down + up).</summary>
    KeyPress,
    /// <summary>Key down only.</summary>
    KeyDown,
    /// <summary>Key up only.</summary>
    KeyUp,
}
