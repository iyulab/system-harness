using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class UIAutomationTools(IHarness harness)
{
    [McpServerTool(Name = "ui_get_focused"), Description("Get the currently focused UI element.")]
    public async Task<string> GetFocusedAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var element = await harness.UIAutomation.GetFocusedElementAsync(ct);
        return McpResponse.Ok(SerializeElement(element), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_get_tree"), Description("Get the accessibility tree of a window. Returns structured JSON with UI elements.")]
    public async Task<string> GetTreeAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Maximum tree depth to traverse (default 3).")] int maxDepth = 3,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var tree = await harness.UIAutomation.GetAccessibilityTreeAsync(titleOrHandle, maxDepth, ct);
        return McpResponse.Ok(SerializeElement(tree), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_find"), Description("Find UI elements matching criteria (name, automationId, controlType, className).")]
    public async Task<string> FindAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Element name to match.")] string? name = null,
        [Description("Automation ID to match.")] string? automationId = null,
        [Description("Control type: Button, Edit, ComboBox, CheckBox, RadioButton, List, ListItem, Tree, TreeItem, Tab, TabItem, Menu, MenuItem, Text, Window, Group, Pane, etc.")] string? controlType = null,
        [Description("Window class name to match.")] string? className = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var condition = new UIElementCondition
        {
            Name = name,
            AutomationId = automationId,
            ClassName = className,
        };
        if (controlType is not null && Enum.TryParse<UIControlType>(controlType, ignoreCase: true, out var ct2))
            condition.ControlType = ct2;

        var elements = await harness.UIAutomation.FindAllAsync(titleOrHandle, condition, ct);
        return McpResponse.Items(elements.Select(SerializeElement).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_click"), Description("Click a UI element by its name or automation ID within a window.")]
    public async Task<string> ClickAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Element name to find.")] string? name = null,
        [Description("Automation ID to find.")] string? automationId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var element = await FindSingleElement(titleOrHandle, name, automationId, ct);
        if (element is null)
            return McpResponse.Error("element_not_found", $"UI element not found: name='{name}', automationId='{automationId}' in window '{titleOrHandle}'", sw.ElapsedMilliseconds);
        await harness.UIAutomation.ClickElementAsync(element, ct);
        ActionLog.Record("ui_click", $"element='{element.Name}'", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Clicked element: {element.Name}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_set_value"), Description("Set the text value of a UI element (text box, etc.).")]
    public async Task<string> SetValueAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Text value to set.")] string value,
        [Description("Element name to find.")] string? name = null,
        [Description("Automation ID to find.")] string? automationId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var element = await FindSingleElement(titleOrHandle, name, automationId, ct);
        if (element is null)
            return McpResponse.Error("element_not_found", $"UI element not found: name='{name}', automationId='{automationId}' in window '{titleOrHandle}'", sw.ElapsedMilliseconds);
        await harness.UIAutomation.SetValueAsync(element, value, ct);
        ActionLog.Record("ui_set_value", $"element='{element.Name}', value='{value}'", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Set value of '{element.Name}' to '{value}'.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_type_into"), Description(
        "Find a UI element, click to focus it, select all existing text, then type new text via keyboard. " +
        "More reliable than ui_set_value for controls that don't support the Value pattern. " +
        "Combines: find element → click center → Ctrl+A → type text.")]
    public async Task<string> TypeIntoAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Text to type into the element.")] string text,
        [Description("Element name to find.")] string? name = null,
        [Description("Automation ID to find.")] string? automationId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var element = await FindSingleElement(titleOrHandle, name, automationId, ct);
        if (element is null)
            return McpResponse.Error("element_not_found", $"UI element not found: name='{name}', automationId='{automationId}' in window '{titleOrHandle}'", sw.ElapsedMilliseconds);

        // Click center of element to focus it
        var cx = element.BoundingRectangle.CenterX;
        var cy = element.BoundingRectangle.CenterY;
        await harness.Mouse.ClickAsync(cx, cy, MouseButton.Left, ct);
        await Task.Delay(50, ct); // Brief settle

        // Select all existing text, then type replacement
        await harness.Keyboard.HotkeyAsync(ct, Key.Ctrl, Key.A);
        await Task.Delay(30, ct);
        await harness.Keyboard.TypeAsync(text, ct: ct);
        ActionLog.Record("ui_type_into", $"element='{element.Name}', text='{text}'", sw.ElapsedMilliseconds, true);

        return McpResponse.Ok(new
        {
            element = new { element.Name, element.AutomationId, controlType = element.ControlType.ToString() },
            clickedAt = new { x = cx, y = cy },
            typedText = text,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_invoke"), Description("Invoke (activate) a UI element's default action (button press, menu click).")]
    public async Task<string> InvokeAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Element name to find.")] string? name = null,
        [Description("Automation ID to find.")] string? automationId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var element = await FindSingleElement(titleOrHandle, name, automationId, ct);
        if (element is null)
            return McpResponse.Error("element_not_found", $"UI element not found: name='{name}', automationId='{automationId}' in window '{titleOrHandle}'", sw.ElapsedMilliseconds);
        await harness.UIAutomation.InvokeAsync(element, ct);
        ActionLog.Record("ui_invoke", $"element='{element.Name}'", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Invoked element: {element.Name}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_select"), Description("Select an item by text within a selection control (combo box, list box, etc.).")]
    public async Task<string> SelectAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Text of the item to select.")] string itemText,
        [Description("Element name to find.")] string? name = null,
        [Description("Automation ID to find.")] string? automationId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (string.IsNullOrWhiteSpace(itemText))
            return McpResponse.Error("invalid_parameter", "itemText cannot be empty.", sw.ElapsedMilliseconds);
        var element = await FindSingleElement(titleOrHandle, name, automationId, ct);
        if (element is null)
            return McpResponse.Error("element_not_found", $"UI element not found: name='{name}', automationId='{automationId}' in window '{titleOrHandle}'", sw.ElapsedMilliseconds);
        await harness.UIAutomation.SelectAsync(element, itemText, ct);
        ActionLog.Record("ui_select", $"element='{element.Name}', item='{itemText}'", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Selected '{itemText}' in '{element.Name}'.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_expand"), Description("Expand a collapsible UI element (tree node, combo box dropdown, menu, etc.).")]
    public async Task<string> ExpandAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Element name to find.")] string? name = null,
        [Description("Automation ID to find.")] string? automationId = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var element = await FindSingleElement(titleOrHandle, name, automationId, ct);
        if (element is null)
            return McpResponse.Error("element_not_found", $"UI element not found: name='{name}', automationId='{automationId}' in window '{titleOrHandle}'", sw.ElapsedMilliseconds);
        await harness.UIAutomation.ExpandAsync(element, ct);
        ActionLog.Record("ui_expand", $"element='{element.Name}'", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Expanded element: {element.Name}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_wait_element"), Description(
        "Poll the accessibility tree until an element matching criteria appears or timeout is reached. " +
        "Useful for waiting on dynamically loaded UI elements.")]
    public async Task<string> WaitElementAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Element name to match.")] string? name = null,
        [Description("Automation ID to match.")] string? automationId = null,
        [Description("Control type: Button, Edit, ComboBox, CheckBox, RadioButton, List, ListItem, Tree, TreeItem, Tab, TabItem, Menu, MenuItem, Text, etc.")] string? controlType = null,
        [Description("Maximum time to wait in milliseconds.")] int timeoutMs = 10000,
        [Description("Polling interval in milliseconds (minimum 100).")] int intervalMs = 500,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);
        var condition = new UIElementCondition { Name = name, AutomationId = automationId };
        if (controlType is not null && Enum.TryParse<UIControlType>(controlType, ignoreCase: true, out var ct2))
            condition.ControlType = ct2;

        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(Math.Max(intervalMs, 100));
        var attempts = 0;

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            attempts++;

            var element = await harness.UIAutomation.FindFirstAsync(titleOrHandle, condition, ct);
            if (element is not null)
            {
                return McpResponse.Ok(new
                {
                    found = true,
                    element = SerializeElement(element),
                    attempts,
                }, sw.ElapsedMilliseconds);
            }

            var remaining = deadline - sw.Elapsed;
            if (remaining <= TimeSpan.Zero) break;
            await Task.Delay(remaining < interval ? remaining : interval, ct);
        }

        return McpResponse.Ok(new
        {
            found = false,
            element = (object?)null,
            attempts,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_select_menu"), Description(
        "Navigate a menu path like 'File>Save As' by clicking each item in sequence. " +
        "Splits the path by '>' and clicks each menu item with a brief delay for submenus to appear. " +
        "Returns the list of clicked items.")]
    public async Task<string> SelectMenuAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Menu path with '>' separator (e.g., 'File>Save As', 'Edit>Find').")] string menuPath,
        [Description("Delay between menu clicks in milliseconds (for submenus to appear).")] int delayMs = 200,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (string.IsNullOrWhiteSpace(menuPath))
            return McpResponse.Error("invalid_parameter", "menuPath cannot be empty.", sw.ElapsedMilliseconds);
        var segments = menuPath.Split('>')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        if (segments.Length == 0)
            return McpResponse.Error("empty_menu_path", "Menu path is empty.", sw.ElapsedMilliseconds);

        var clicked = new List<object>();

        foreach (var segment in segments)
        {
            var condition = new UIElementCondition { Name = segment, ControlType = UIControlType.MenuItem };
            var element = await harness.UIAutomation.FindFirstAsync(titleOrHandle, condition, ct);

            if (element is null)
            {
                // Try MenuBar or Menu item without strict type
                condition = new UIElementCondition { Name = segment };
                element = await harness.UIAutomation.FindFirstAsync(titleOrHandle, condition, ct);
            }

            if (element is null)
                return McpResponse.Error("menu_item_not_found",
                    $"Menu item '{segment}' not found in path '{menuPath}'. Clicked so far: {clicked.Count}.",
                    sw.ElapsedMilliseconds);

            await harness.UIAutomation.InvokeAsync(element, ct);
            clicked.Add(new { element.Name, controlType = element.ControlType.ToString() });
            await Task.Delay(Math.Max(delayMs, 50), ct);
        }

        ActionLog.Record("ui_select_menu", $"path='{menuPath}', clicked={clicked.Count}", sw.ElapsedMilliseconds, true);
        return McpResponse.Ok(new
        {
            menuPath,
            clickedItems = clicked,
            clickCount = clicked.Count,
        }, sw.ElapsedMilliseconds);
    }

    private static readonly UIControlType[] ClickableTypes =
    [
        UIControlType.Button, UIControlType.Hyperlink, UIControlType.MenuItem,
        UIControlType.CheckBox, UIControlType.RadioButton, UIControlType.TabItem,
        UIControlType.SplitButton, UIControlType.ListItem, UIControlType.TreeItem,
    ];

    private static readonly UIControlType[] InputTypes =
    [
        UIControlType.Edit, UIControlType.ComboBox, UIControlType.Spinner,
        UIControlType.Slider,
    ];

    [McpServerTool(Name = "ui_annotate"), Description(
        "Number all interactive UI elements in a window. " +
        "Returns a numbered list of clickable and input elements with names, types, and center coordinates. " +
        "AI can reference elements by number for subsequent actions.")]
    public async Task<string> AnnotateAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var tree = await harness.UIAutomation.GetAccessibilityTreeAsync(titleOrHandle, 5, ct);

        var interactiveTypes = ClickableTypes.Concat(InputTypes).ToHashSet();
        var elements = FlattenElements(tree)
            .Where(e => interactiveTypes.Contains(e.ControlType) && e.IsEnabled && !e.IsOffscreen)
            .ToArray();

        var numbered = elements.Select((e, i) => new
        {
            index = i + 1,
            e.Name,
            e.AutomationId,
            controlType = e.ControlType.ToString(),
            center = new { x = e.BoundingRectangle.CenterX, y = e.BoundingRectangle.CenterY },
            bounds = new { e.BoundingRectangle.X, e.BoundingRectangle.Y, e.BoundingRectangle.Width, e.BoundingRectangle.Height },
            e.Value,
        }).ToArray();

        return McpResponse.Ok(new
        {
            elements = numbered,
            count = numbered.Length,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_detect_clickables"), Description(
        "Detect all clickable UI elements in a window (buttons, links, checkboxes, menu items, etc.). " +
        "Returns elements with names, bounding rectangles, and control types.")]
    public async Task<string> DetectClickablesAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var tree = await harness.UIAutomation.GetAccessibilityTreeAsync(titleOrHandle, 5, ct);
        var clickables = FlattenElements(tree)
            .Where(e => ClickableTypes.Contains(e.ControlType) && e.IsEnabled && !e.IsOffscreen)
            .Select(SerializeCompact)
            .ToArray();
        return McpResponse.Items(clickables, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_detect_inputs"), Description(
        "Detect all input UI elements in a window (text boxes, combo boxes, sliders, etc.). " +
        "Returns elements with names, current values, bounding rectangles, and control types.")]
    public async Task<string> DetectInputsAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var tree = await harness.UIAutomation.GetAccessibilityTreeAsync(titleOrHandle, 5, ct);
        var inputs = FlattenElements(tree)
            .Where(e => InputTypes.Contains(e.ControlType) && e.IsEnabled && !e.IsOffscreen)
            .Select(e => new
            {
                e.Name, e.AutomationId, controlType = e.ControlType.ToString(),
                e.Value,
                bounds = new { e.BoundingRectangle.X, e.BoundingRectangle.Y, e.BoundingRectangle.Width, e.BoundingRectangle.Height },
            })
            .ToArray();
        return McpResponse.Items(inputs, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "ui_get_at"), Description(
        "Get the UI element at specific screen coordinates. " +
        "Finds the deepest element whose bounding rectangle contains the given point.")]
    public async Task<string> GetAtAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("Absolute screen X coordinate.")] int x,
        [Description("Absolute screen Y coordinate.")] int y,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        var tree = await harness.UIAutomation.GetAccessibilityTreeAsync(titleOrHandle, 5, ct);
        // Find deepest element containing the point
        var match = FlattenElements(tree)
            .Where(e => e.BoundingRectangle.Contains(x, y) && !e.IsOffscreen)
            .OrderByDescending(e => e.BoundingRectangle.X + e.BoundingRectangle.Y) // Deeper = smaller = later
            .ThenBy(e => e.BoundingRectangle.Width * e.BoundingRectangle.Height) // Smallest area = most specific
            .FirstOrDefault();

        if (match is null)
            return McpResponse.Ok(new { found = false, x, y }, sw.ElapsedMilliseconds);

        return McpResponse.Ok(new
        {
            found = true,
            element = SerializeElement(match),
            point = new { x, y },
        }, sw.ElapsedMilliseconds);
    }

    private static IEnumerable<UIElement> FlattenElements(UIElement root)
    {
        yield return root;
        foreach (var child in root.Children)
            foreach (var descendant in FlattenElements(child))
                yield return descendant;
    }

    private static object SerializeCompact(UIElement e) => new
    {
        e.Name, e.AutomationId, controlType = e.ControlType.ToString(),
        bounds = new { e.BoundingRectangle.X, e.BoundingRectangle.Y, e.BoundingRectangle.Width, e.BoundingRectangle.Height },
    };

    private async Task<UIElement?> FindSingleElement(
        string titleOrHandle, string? name, string? automationId, CancellationToken ct)
    {
        var condition = new UIElementCondition { Name = name, AutomationId = automationId };
        return await harness.UIAutomation.FindFirstAsync(titleOrHandle, condition, ct);
    }

    private static object SerializeElement(UIElement e) => new
    {
        e.Name, e.AutomationId, controlType = e.ControlType.ToString(),
        bounds = new { e.BoundingRectangle.X, e.BoundingRectangle.Y, e.BoundingRectangle.Width, e.BoundingRectangle.Height },
        e.IsEnabled, e.IsOffscreen, e.Value, e.ClassName,
        children = e.Children.Select(SerializeElement).ToList(),
    };
}
