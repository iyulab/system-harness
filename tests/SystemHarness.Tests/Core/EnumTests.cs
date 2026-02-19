namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class EnumTests
{
    [Fact]
    public void UIControlType_HasExpectedCount()
    {
        var values = Enum.GetValues<UIControlType>();
        Assert.Equal(39, values.Length);
    }

    [Fact]
    public void UIControlType_CommonValues()
    {
        Assert.True(Enum.IsDefined(UIControlType.Unknown));
        Assert.True(Enum.IsDefined(UIControlType.Button));
        Assert.True(Enum.IsDefined(UIControlType.CheckBox));
        Assert.True(Enum.IsDefined(UIControlType.ComboBox));
        Assert.True(Enum.IsDefined(UIControlType.Edit));
        Assert.True(Enum.IsDefined(UIControlType.List));
        Assert.True(Enum.IsDefined(UIControlType.Menu));
        Assert.True(Enum.IsDefined(UIControlType.MenuItem));
        Assert.True(Enum.IsDefined(UIControlType.Tab));
        Assert.True(Enum.IsDefined(UIControlType.Text));
        Assert.True(Enum.IsDefined(UIControlType.Tree));
        Assert.True(Enum.IsDefined(UIControlType.Window));
        Assert.True(Enum.IsDefined(UIControlType.Custom));
    }

    [Fact]
    public void WindowState_HasExpectedCount()
    {
        var values = Enum.GetValues<WindowState>();
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void MouseButton_HasExpectedCount()
    {
        var values = Enum.GetValues<MouseButton>();
        Assert.Equal(3, values.Length);
    }

    [Fact]
    public void ImageFormat_HasExpectedCount()
    {
        var values = Enum.GetValues<ImageFormat>();
        Assert.Equal(2, values.Length);
    }

    [Fact]
    public void Key_HasValues()
    {
        // Key enum should have a substantial number of entries
        var values = Enum.GetValues<Key>();
        Assert.True(values.Length >= 50, $"Expected at least 50 Key values, got {values.Length}");
        Assert.Contains(Key.Enter, values);
        Assert.Contains(Key.Escape, values);
        Assert.Contains(Key.Tab, values);
        Assert.Contains(Key.Space, values);
    }
}
