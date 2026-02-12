using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IUIAutomation"/> using FlaUI (UIA3).
/// </summary>
public sealed class WindowsUIAutomation : IUIAutomation, IDisposable
{
    private readonly UIA3Automation _automation = new();

    /// <inheritdoc />
    public Task<UIElement> GetFocusedElementAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var focused = _automation.FocusedElement();
            return ToUIElement(focused, maxDepth: 0);
        }, ct);
    }

    /// <inheritdoc />
    public async Task<UIElement> GetRootElementAsync(string titleOrHandle, CancellationToken ct = default)
    {
        var element = await FindAutomationElementAsync(titleOrHandle, ct);
        return ToUIElement(element, maxDepth: 0);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UIElement>> FindAllAsync(
        string titleOrHandle, UIElementCondition condition, CancellationToken ct = default)
    {
        var root = await FindAutomationElementAsync(titleOrHandle, ct);
        var builtCondition = BuildCondition(condition);

        return await Task.Run(() =>
        {
            var found = root.FindAll(TreeScope.Descendants, builtCondition);
            return found.Select(e => ToUIElement(e, maxDepth: 0)).ToList();
        }, ct);
    }

    /// <inheritdoc />
    public async Task<UIElement?> FindFirstAsync(
        string titleOrHandle, UIElementCondition condition, CancellationToken ct = default)
    {
        var root = await FindAutomationElementAsync(titleOrHandle, ct);
        var builtCondition = BuildCondition(condition);

        return await Task.Run(() =>
        {
            var found = root.FindFirst(TreeScope.Descendants, builtCondition);
            return found is null ? null : ToUIElement(found, maxDepth: 0);
        }, ct);
    }

    /// <inheritdoc />
    public async Task<UIElement> GetAccessibilityTreeAsync(
        string titleOrHandle, int maxDepth = 5, CancellationToken ct = default)
    {
        var root = await FindAutomationElementAsync(titleOrHandle, ct);
        return await Task.Run(() => ToUIElement(root, maxDepth), ct);
    }

    /// <inheritdoc />
    public Task ClickElementAsync(UIElement element, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var auto = ResolveElement(element);
            auto.Click();
        }, ct);
    }

    /// <inheritdoc />
    public Task SetValueAsync(UIElement element, string value, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var auto = ResolveElement(element);

            if (auto.Patterns.Value.IsSupported)
            {
                auto.Patterns.Value.Pattern.SetValue(value);
                return;
            }

            // Fallback: focus and type
            auto.Focus();
            auto.AsTextBox().Text = value;
        }, ct);
    }

    /// <inheritdoc />
    public Task InvokeAsync(UIElement element, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var auto = ResolveElement(element);

            if (auto.Patterns.Invoke.IsSupported)
            {
                auto.Patterns.Invoke.Pattern.Invoke();
                return;
            }

            // Fallback: click
            auto.Click();
        }, ct);
    }

    /// <inheritdoc />
    public Task SelectAsync(UIElement element, string itemText, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var auto = ResolveElement(element);

            // Try ComboBox first
            if (auto.Patterns.ExpandCollapse.IsSupported)
            {
                auto.Patterns.ExpandCollapse.Pattern.Expand();
            }

            // Search descendants for matching text and invoke SelectionItem pattern
            var child = auto.FindFirst(TreeScope.Descendants,
                auto.ConditionFactory.ByName(itemText));
            if (child is not null && child.Patterns.SelectionItem.IsSupported)
            {
                child.Patterns.SelectionItem.Pattern.Select();
                return;
            }

            // Try clicking the item directly
            if (child is not null)
            {
                child.Click();
                return;
            }

            throw new HarnessException($"Could not select item '{itemText}' in element '{element.Name}'.");
        }, ct);
    }

    /// <inheritdoc />
    public Task ExpandAsync(UIElement element, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var auto = ResolveElement(element);

            if (auto.Patterns.ExpandCollapse.IsSupported)
            {
                auto.Patterns.ExpandCollapse.Pattern.Expand();
                return;
            }

            throw new HarnessException($"Element '{element.Name}' does not support expand/collapse.");
        }, ct);
    }

    public void Dispose()
    {
        _automation.Dispose();
    }

    // --- Private helpers ---

    private async Task<AutomationElement> FindAutomationElementAsync(string titleOrHandle, CancellationToken ct)
    {
        var hwnd = await WindowHandleResolver.ResolveAsync(titleOrHandle, ct);
        var element = _automation.FromHandle((nint)hwnd);
        return element ?? throw new HarnessException($"Could not get automation element for window: {titleOrHandle}");
    }

    private AutomationElement ResolveElement(UIElement element)
    {
        // Try by handle + automation ID (most reliable)
        if (element.Handle != 0)
        {
            var windowElement = _automation.FromHandle(element.Handle);
            if (windowElement is not null)
            {
                // If the element IS the window root, return it directly
                if (string.IsNullOrEmpty(element.AutomationId) && string.IsNullOrEmpty(element.Name))
                    return windowElement;

                if (!string.IsNullOrEmpty(element.AutomationId))
                {
                    var found = windowElement.FindFirst(TreeScope.Descendants,
                        windowElement.ConditionFactory.ByAutomationId(element.AutomationId));
                    if (found is not null)
                        return found;
                }

                if (!string.IsNullOrEmpty(element.Name))
                {
                    // Build a more specific condition when we have control type
                    ConditionBase condition;
                    if (element.ControlType != UIControlType.Unknown)
                    {
                        condition = new AndCondition(
                            windowElement.ConditionFactory.ByName(element.Name),
                            windowElement.ConditionFactory.ByControlType(MapControlType(element.ControlType)));
                    }
                    else
                    {
                        condition = windowElement.ConditionFactory.ByName(element.Name);
                    }

                    var found = windowElement.FindFirst(TreeScope.Descendants, condition);
                    if (found is not null)
                        return found;
                }
            }
        }

        // Last resort: search desktop by automation ID or name
        var desktop = _automation.GetDesktop();

        if (!string.IsNullOrEmpty(element.AutomationId))
        {
            var found = desktop.FindFirst(TreeScope.Descendants,
                desktop.ConditionFactory.ByAutomationId(element.AutomationId));
            if (found is not null)
                return found;
        }

        throw new HarnessException($"Could not resolve UI element: {element.Name} (AutomationId={element.AutomationId})");
    }

    private ConditionBase BuildCondition(UIElementCondition condition)
    {
        var cf = _automation.ConditionFactory;
        var conditions = new List<ConditionBase>();

        if (!string.IsNullOrEmpty(condition.Name))
            conditions.Add(cf.ByName(condition.Name));

        if (!string.IsNullOrEmpty(condition.AutomationId))
            conditions.Add(cf.ByAutomationId(condition.AutomationId));

        if (condition.ControlType.HasValue)
            conditions.Add(cf.ByControlType(MapControlType(condition.ControlType.Value)));

        if (!string.IsNullOrEmpty(condition.ClassName))
            conditions.Add(cf.ByClassName(condition.ClassName));

        return conditions.Count switch
        {
            0 => cf.ByControlType(ControlType.Window),
            1 => conditions[0],
            _ => new AndCondition(conditions.ToArray())
        };
    }

    private static UIElement ToUIElement(AutomationElement element, int maxDepth)
    {
        var children = new List<UIElement>();

        if (maxDepth > 0)
        {
            try
            {
                var childElements = element.FindAll(TreeScope.Children, TrueCondition.Default);

                foreach (var child in childElements)
                {
                    try
                    {
                        children.Add(ToUIElement(child, maxDepth - 1));
                    }
                    catch
                    {
                        // Skip elements that throw during property access
                    }
                }
            }
            catch
            {
                // Skip if children enumeration fails
            }
        }

        string? value = null;
        try
        {
            if (element.Patterns.Value.IsSupported)
                value = element.Patterns.Value.Pattern.Value.ValueOrDefault;
        }
        catch { /* Ignore */ }

        return new UIElement
        {
            Name = element.Properties.Name.ValueOrDefault ?? string.Empty,
            AutomationId = element.Properties.AutomationId.ValueOrDefault,
            ControlType = MapFromControlType(element.Properties.ControlType.ValueOrDefault),
            BoundingRectangle = MapRect(element.Properties.BoundingRectangle.ValueOrDefault),
            IsEnabled = element.Properties.IsEnabled.ValueOrDefault,
            IsOffscreen = element.Properties.IsOffscreen.ValueOrDefault,
            Value = value,
            ClassName = element.Properties.ClassName.ValueOrDefault,
            ProcessId = element.Properties.ProcessId.ValueOrDefault,
            Handle = element.Properties.NativeWindowHandle.ValueOrDefault,
            Children = children,
        };
    }

    private static Rectangle MapRect(System.Drawing.Rectangle rect)
    {
        return new Rectangle(rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static UIControlType MapFromControlType(ControlType ct)
    {
        if (ct == ControlType.Button) return UIControlType.Button;
        if (ct == ControlType.Calendar) return UIControlType.Calendar;
        if (ct == ControlType.CheckBox) return UIControlType.CheckBox;
        if (ct == ControlType.ComboBox) return UIControlType.ComboBox;
        if (ct == ControlType.DataGrid) return UIControlType.DataGrid;
        if (ct == ControlType.DataItem) return UIControlType.DataItem;
        if (ct == ControlType.Document) return UIControlType.Document;
        if (ct == ControlType.Edit) return UIControlType.Edit;
        if (ct == ControlType.Group) return UIControlType.Group;
        if (ct == ControlType.Header) return UIControlType.Header;
        if (ct == ControlType.HeaderItem) return UIControlType.HeaderItem;
        if (ct == ControlType.Hyperlink) return UIControlType.Hyperlink;
        if (ct == ControlType.Image) return UIControlType.Image;
        if (ct == ControlType.List) return UIControlType.List;
        if (ct == ControlType.ListItem) return UIControlType.ListItem;
        if (ct == ControlType.Menu) return UIControlType.Menu;
        if (ct == ControlType.MenuBar) return UIControlType.MenuBar;
        if (ct == ControlType.MenuItem) return UIControlType.MenuItem;
        if (ct == ControlType.Pane) return UIControlType.Pane;
        if (ct == ControlType.ProgressBar) return UIControlType.ProgressBar;
        if (ct == ControlType.RadioButton) return UIControlType.RadioButton;
        if (ct == ControlType.ScrollBar) return UIControlType.ScrollBar;
        if (ct == ControlType.Separator) return UIControlType.Separator;
        if (ct == ControlType.Slider) return UIControlType.Slider;
        if (ct == ControlType.Spinner) return UIControlType.Spinner;
        if (ct == ControlType.SplitButton) return UIControlType.SplitButton;
        if (ct == ControlType.StatusBar) return UIControlType.StatusBar;
        if (ct == ControlType.Tab) return UIControlType.Tab;
        if (ct == ControlType.TabItem) return UIControlType.TabItem;
        if (ct == ControlType.Table) return UIControlType.Table;
        if (ct == ControlType.Text) return UIControlType.Text;
        if (ct == ControlType.TitleBar) return UIControlType.TitleBar;
        if (ct == ControlType.ToolBar) return UIControlType.ToolBar;
        if (ct == ControlType.ToolTip) return UIControlType.ToolTip;
        if (ct == ControlType.Tree) return UIControlType.Tree;
        if (ct == ControlType.TreeItem) return UIControlType.TreeItem;
        if (ct == ControlType.Window) return UIControlType.Window;
        return UIControlType.Unknown;
    }

    private static ControlType MapControlType(UIControlType ct)
    {
        return ct switch
        {
            UIControlType.Button => ControlType.Button,
            UIControlType.Calendar => ControlType.Calendar,
            UIControlType.CheckBox => ControlType.CheckBox,
            UIControlType.ComboBox => ControlType.ComboBox,
            UIControlType.DataGrid => ControlType.DataGrid,
            UIControlType.DataItem => ControlType.DataItem,
            UIControlType.Document => ControlType.Document,
            UIControlType.Edit => ControlType.Edit,
            UIControlType.Group => ControlType.Group,
            UIControlType.Header => ControlType.Header,
            UIControlType.HeaderItem => ControlType.HeaderItem,
            UIControlType.Hyperlink => ControlType.Hyperlink,
            UIControlType.Image => ControlType.Image,
            UIControlType.List => ControlType.List,
            UIControlType.ListItem => ControlType.ListItem,
            UIControlType.Menu => ControlType.Menu,
            UIControlType.MenuBar => ControlType.MenuBar,
            UIControlType.MenuItem => ControlType.MenuItem,
            UIControlType.Pane => ControlType.Pane,
            UIControlType.ProgressBar => ControlType.ProgressBar,
            UIControlType.RadioButton => ControlType.RadioButton,
            UIControlType.ScrollBar => ControlType.ScrollBar,
            UIControlType.Separator => ControlType.Separator,
            UIControlType.Slider => ControlType.Slider,
            UIControlType.Spinner => ControlType.Spinner,
            UIControlType.SplitButton => ControlType.SplitButton,
            UIControlType.StatusBar => ControlType.StatusBar,
            UIControlType.Tab => ControlType.Tab,
            UIControlType.TabItem => ControlType.TabItem,
            UIControlType.Table => ControlType.Table,
            UIControlType.Text => ControlType.Text,
            UIControlType.TitleBar => ControlType.TitleBar,
            UIControlType.ToolBar => ControlType.ToolBar,
            UIControlType.ToolTip => ControlType.ToolTip,
            UIControlType.Tree => ControlType.Tree,
            UIControlType.TreeItem => ControlType.TreeItem,
            UIControlType.Window => ControlType.Window,
            _ => ControlType.Custom
        };
    }
}
