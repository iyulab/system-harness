using ChildProcessGuard;
using SystemHarness.Windows;

namespace SystemHarness.Tests.UIAutomation;

public class UIAutomationNotepadFixture : IAsyncLifetime
{
    public ProcessGuardian Guardian { get; } = new();
    public int NotepadPid { get; private set; }

    public async Task InitializeAsync()
    {
        var proc = Guardian.StartProcess("notepad.exe");
        NotepadPid = proc.Id;
        await Task.Delay(2000);
    }

    public async Task DisposeAsync()
    {
        await NotepadHelper.CloseNotepadByPidAsync(NotepadPid);
        await Guardian.KillAllProcessesAsync();
        Guardian.Dispose();
    }
}

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class WindowsUIAutomationTests : IClassFixture<UIAutomationNotepadFixture>, IDisposable
{
    private readonly WindowsUIAutomation _uia = new();

    public WindowsUIAutomationTests(UIAutomationNotepadFixture fixture, DesktopInteractionFixture collectionFixture)
    {
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _uia.Dispose();
    }

    [Fact]
    public async Task GetFocusedElement_ReturnsElement()
    {
        var element = await _uia.GetFocusedElementAsync();

        Assert.NotNull(element);
        Assert.NotNull(element.Name);
    }

    [Fact]
    public async Task GetRootElement_WithNotepad_ReturnsWindowElement()
    {
        var root = await _uia.GetRootElementAsync("Notepad");

        Assert.NotNull(root);
        Assert.Contains("Notepad", root.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UIControlType.Window, root.ControlType);
    }

    [Fact]
    public async Task GetAccessibilityTree_HasChildren()
    {
        var tree = await _uia.GetAccessibilityTreeAsync("Notepad", maxDepth: 2);

        Assert.NotNull(tree);
        Assert.NotEmpty(tree.Children);
    }

    [Fact]
    public async Task GetAccessibilityTree_DepthZero_NoChildren()
    {
        var tree = await _uia.GetAccessibilityTreeAsync("Notepad", maxDepth: 0);

        Assert.NotNull(tree);
        Assert.Empty(tree.Children);
    }

    [Fact]
    public async Task FindAll_ByControlType_FindsElements()
    {
        var condition = new UIElementCondition { ControlType = UIControlType.MenuItem };
        var elements = await _uia.FindAllAsync("Notepad", condition);

        // Notepad has menu items (File, Edit, Format, View, Help)
        Assert.NotEmpty(elements);
        Assert.All(elements, e => Assert.Equal(UIControlType.MenuItem, e.ControlType));
    }

    [Fact]
    public async Task FindFirst_ByControlType_EditOrDocument_FindsTextArea()
    {
        // Windows 10 Notepad uses Edit; Windows 11 Notepad uses Document
        var editCondition = new UIElementCondition { ControlType = UIControlType.Edit };
        var docCondition = new UIElementCondition { ControlType = UIControlType.Document };

        var edit = await _uia.FindFirstAsync("Notepad", editCondition);
        var doc = await _uia.FindFirstAsync("Notepad", docCondition);

        // At least one should exist
        Assert.True(edit is not null || doc is not null,
            "Notepad should have an Edit or Document control for its text area.");
    }

    [Fact]
    public async Task FindFirst_NonExistent_ReturnsNull()
    {
        var condition = new UIElementCondition { AutomationId = "NonExistentId_XYZ_99999" };
        var element = await _uia.FindFirstAsync("Notepad", condition);

        Assert.Null(element);
    }

    [Fact]
    public async Task GetRootElement_NonExistentWindow_ThrowsHarnessException()
    {
        await Assert.ThrowsAsync<HarnessException>(() =>
            _uia.GetRootElementAsync("NonExistentWindow_XYZ_99999"));
    }

    [Fact]
    public async Task UIElement_HasBoundingRectangle()
    {
        var root = await _uia.GetRootElementAsync("Notepad");

        Assert.True(root.BoundingRectangle.Width > 0);
        Assert.True(root.BoundingRectangle.Height > 0);
    }

    [Fact]
    public async Task UIElement_IsEnabled()
    {
        var root = await _uia.GetRootElementAsync("Notepad");

        Assert.True(root.IsEnabled);
    }
}
