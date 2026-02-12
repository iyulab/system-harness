namespace SystemHarness;

/// <summary>
/// Search condition for finding UI elements. Multiple properties combine with AND logic.
/// </summary>
public sealed class UIElementCondition
{
    /// <summary>
    /// Match by element name (substring, case-insensitive).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Match by automation identifier (exact, case-insensitive).
    /// </summary>
    public string? AutomationId { get; set; }

    /// <summary>
    /// Match by control type.
    /// </summary>
    public UIControlType? ControlType { get; set; }

    /// <summary>
    /// Match by class name (exact, case-insensitive).
    /// </summary>
    public string? ClassName { get; set; }
}
