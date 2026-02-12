namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class UIModelTests
{
    // --- UIElement ---

    [Fact]
    public void UIElement_RequiredProperties()
    {
        var element = new UIElement { Name = "OK" };
        Assert.Equal("OK", element.Name);
        Assert.Null(element.AutomationId);
        Assert.Null(element.Value);
        Assert.Null(element.ClassName);
        Assert.False(element.IsEnabled);
        Assert.False(element.IsOffscreen);
        Assert.Empty(element.Children);
    }

    [Fact]
    public void UIElement_FullyPopulated()
    {
        var element = new UIElement
        {
            Name = "Save",
            AutomationId = "btnSave",
            ControlType = UIControlType.Button,
            BoundingRectangle = new Rectangle(100, 200, 80, 30),
            IsEnabled = true,
            IsOffscreen = false,
            Value = null,
            ClassName = "Button",
            ProcessId = 1234,
            Handle = 0x1000,
            Children = [],
        };

        Assert.Equal("Save", element.Name);
        Assert.Equal("btnSave", element.AutomationId);
        Assert.Equal(UIControlType.Button, element.ControlType);
        Assert.True(element.IsEnabled);
        Assert.Equal(1234, element.ProcessId);
    }

    [Fact]
    public void UIElement_WithChildren()
    {
        var child1 = new UIElement { Name = "Item 1" };
        var child2 = new UIElement { Name = "Item 2" };
        var parent = new UIElement
        {
            Name = "List",
            ControlType = UIControlType.List,
            Children = [child1, child2],
        };

        Assert.Equal(2, parent.Children.Count);
        Assert.Equal("Item 1", parent.Children[0].Name);
    }

    // --- UIElementCondition ---

    [Fact]
    public void UIElementCondition_Defaults_AllNull()
    {
        var condition = new UIElementCondition();
        Assert.Null(condition.Name);
        Assert.Null(condition.AutomationId);
        Assert.Null(condition.ControlType);
        Assert.Null(condition.ClassName);
    }

    [Fact]
    public void UIElementCondition_ByNameAndType()
    {
        var condition = new UIElementCondition
        {
            Name = "Save",
            ControlType = UIControlType.Button,
        };

        Assert.Equal("Save", condition.Name);
        Assert.Equal(UIControlType.Button, condition.ControlType);
    }

    [Fact]
    public void UIElementCondition_ByAutomationId()
    {
        var condition = new UIElementCondition { AutomationId = "txtInput" };
        Assert.Equal("txtInput", condition.AutomationId);
    }

    // --- RecordedAction ---

    [Fact]
    public void RecordedAction_MouseClick()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var action = new RecordedAction
        {
            Type = RecordedActionType.MouseClick,
            Timestamp = timestamp,
            X = 500,
            Y = 300,
            Button = MouseButton.Left,
        };

        Assert.Equal(RecordedActionType.MouseClick, action.Type);
        Assert.Equal(timestamp, action.Timestamp);
        Assert.Equal(500, action.X);
        Assert.Equal(300, action.Y);
        Assert.Equal(MouseButton.Left, action.Button);
        Assert.Null(action.Key);
        Assert.Null(action.ScrollDelta);
    }

    [Fact]
    public void RecordedAction_KeyPress()
    {
        var action = new RecordedAction
        {
            Type = RecordedActionType.KeyPress,
            Timestamp = DateTimeOffset.UtcNow,
            Key = Key.Enter,
            DelayBefore = TimeSpan.FromMilliseconds(150),
        };

        Assert.Equal(RecordedActionType.KeyPress, action.Type);
        Assert.Equal(Key.Enter, action.Key);
        Assert.Equal(150, action.DelayBefore.TotalMilliseconds);
        Assert.Null(action.X);
        Assert.Null(action.Y);
    }

    [Fact]
    public void RecordedAction_MouseScroll()
    {
        var action = new RecordedAction
        {
            Type = RecordedActionType.MouseScroll,
            Timestamp = DateTimeOffset.UtcNow,
            X = 400,
            Y = 300,
            ScrollDelta = -3,
        };

        Assert.Equal(RecordedActionType.MouseScroll, action.Type);
        Assert.Equal(-3, action.ScrollDelta);
    }

    [Fact]
    public void RecordedActionType_AllValues()
    {
        var values = Enum.GetValues<RecordedActionType>();
        Assert.Equal(8, values.Length);
        Assert.Contains(RecordedActionType.MouseMove, values);
        Assert.Contains(RecordedActionType.MouseClick, values);
        Assert.Contains(RecordedActionType.MouseDown, values);
        Assert.Contains(RecordedActionType.MouseUp, values);
        Assert.Contains(RecordedActionType.MouseScroll, values);
        Assert.Contains(RecordedActionType.KeyPress, values);
        Assert.Contains(RecordedActionType.KeyDown, values);
        Assert.Contains(RecordedActionType.KeyUp, values);
    }

    // --- ObserveOptions ---

    [Fact]
    public void ObserveOptions_Defaults()
    {
        var options = new ObserveOptions();
        Assert.True(options.IncludeScreenshot);
        Assert.True(options.IncludeAccessibilityTree);
        Assert.False(options.IncludeOcr);
        Assert.Equal(5, options.AccessibilityTreeMaxDepth);
        Assert.Null(options.OcrOptions);
    }

    [Fact]
    public void ObserveOptions_CustomValues()
    {
        var options = new ObserveOptions
        {
            IncludeScreenshot = false,
            IncludeAccessibilityTree = false,
            IncludeOcr = true,
            AccessibilityTreeMaxDepth = 10,
            OcrOptions = new OcrOptions { Language = "ko-KR" },
        };

        Assert.False(options.IncludeScreenshot);
        Assert.False(options.IncludeAccessibilityTree);
        Assert.True(options.IncludeOcr);
        Assert.Equal(10, options.AccessibilityTreeMaxDepth);
        Assert.Equal("ko-KR", options.OcrOptions!.Language);
    }
}
