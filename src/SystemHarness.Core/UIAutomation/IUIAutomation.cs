namespace SystemHarness;

/// <summary>
/// UI Automation — access the accessibility tree to structurally navigate and manipulate UI elements.
/// Uses UIA3 on Windows. Provides stable, coordinate-independent element interaction.
/// </summary>
public interface IUIAutomation
{
    /// <summary>
    /// Gets the currently focused UI element.
    /// </summary>
    Task<UIElement> GetFocusedElementAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the root automation element for a window identified by title substring or handle.
    /// </summary>
    Task<UIElement> GetRootElementAsync(string titleOrHandle, CancellationToken ct = default);

    /// <summary>
    /// Finds all elements matching the condition within a window.
    /// </summary>
    Task<IReadOnlyList<UIElement>> FindAllAsync(string titleOrHandle, UIElementCondition condition, CancellationToken ct = default);

    /// <summary>
    /// Finds the first element matching the condition within a window, or null if not found.
    /// </summary>
    Task<UIElement?> FindFirstAsync(string titleOrHandle, UIElementCondition condition, CancellationToken ct = default);

    /// <summary>
    /// Gets the full accessibility tree for a window, limited by depth.
    /// </summary>
    /// <param name="titleOrHandle">Window title substring or handle string.</param>
    /// <param name="maxDepth">Maximum tree depth to traverse (default 5).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UIElement> GetAccessibilityTreeAsync(string titleOrHandle, int maxDepth = 5, CancellationToken ct = default);

    /// <summary>
    /// Clicks a UI element using its bounding rectangle center point.
    /// </summary>
    Task ClickElementAsync(UIElement element, CancellationToken ct = default);

    /// <summary>
    /// Sets the text value of a UI element (text box, combo box, etc.).
    /// </summary>
    Task SetValueAsync(UIElement element, string value, CancellationToken ct = default);

    /// <summary>
    /// Invokes a UI element's default action (e.g., button click, menu item activation).
    /// </summary>
    Task InvokeAsync(UIElement element, CancellationToken ct = default);

    /// <summary>
    /// Selects an item by text within a selection control (combo box, list, etc.).
    /// </summary>
    Task SelectAsync(UIElement element, string itemText, CancellationToken ct = default);

    /// <summary>
    /// Expands a collapsible element (tree node, combo box dropdown, menu, etc.).
    /// </summary>
    Task ExpandAsync(UIElement element, CancellationToken ct = default);
}
