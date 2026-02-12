namespace SystemHarness;

/// <summary>
/// Represents a UI element from the accessibility tree.
/// </summary>
public sealed class UIElement
{
    /// <summary>
    /// Display name of the element (e.g., button label, menu text).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Developer-assigned automation identifier, stable across sessions.
    /// </summary>
    public string? AutomationId { get; init; }

    /// <summary>
    /// The type of control (Button, TextBox, Menu, etc.).
    /// </summary>
    public UIControlType ControlType { get; init; }

    /// <summary>
    /// Bounding rectangle in screen coordinates.
    /// </summary>
    public Rectangle BoundingRectangle { get; init; }

    /// <summary>
    /// Whether the element is enabled for interaction.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Whether the element is off-screen (scrolled out of view).
    /// </summary>
    public bool IsOffscreen { get; init; }

    /// <summary>
    /// Current text value (for text boxes, labels, etc.).
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Implementation-specific class name (e.g., "Button", "TextBlock").
    /// </summary>
    public string? ClassName { get; init; }

    /// <summary>
    /// PID of the process that owns this element.
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// Platform-specific handle to the element's host window.
    /// </summary>
    public nint Handle { get; init; }

    /// <summary>
    /// Child elements in the accessibility tree.
    /// </summary>
    public IReadOnlyList<UIElement> Children { get; init; } = [];
}
