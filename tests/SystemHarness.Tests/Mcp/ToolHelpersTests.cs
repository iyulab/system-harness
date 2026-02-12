using SystemHarness.Mcp.Tools;

namespace SystemHarness.Tests.Mcp;

[Trait("Category", "CI")]
public class ToolHelpersTests
{
    private static readonly IReadOnlyList<WindowInfo> Windows =
    [
        new WindowInfo { Handle = 100, Title = "Notepad - test.txt", ProcessId = 1000, Bounds = new Rectangle(0, 0, 800, 600) },
        new WindowInfo { Handle = 200, Title = "Calculator", ProcessId = 2000, Bounds = new Rectangle(100, 100, 400, 300) },
        new WindowInfo { Handle = 300, Title = "Visual Studio Code", ProcessId = 3000, Bounds = new Rectangle(50, 50, 1200, 800) },
    ];

    [Fact]
    public void FindWindow_ByExactHandle()
    {
        var win = ToolHelpers.FindWindow(Windows, "200");
        Assert.NotNull(win);
        Assert.Equal("Calculator", win.Title);
    }

    [Fact]
    public void FindWindow_ByTitleSubstring()
    {
        var win = ToolHelpers.FindWindow(Windows, "Notepad");
        Assert.NotNull(win);
        Assert.Equal(100, win.Handle);
    }

    [Fact]
    public void FindWindow_ByTitleSubstring_CaseInsensitive()
    {
        var win = ToolHelpers.FindWindow(Windows, "calculator");
        Assert.NotNull(win);
        Assert.Equal(200, win.Handle);
    }

    [Fact]
    public void FindWindow_NotFound_ReturnsNull()
    {
        var win = ToolHelpers.FindWindow(Windows, "Firefox");
        Assert.Null(win);
    }

    [Fact]
    public void FindWindow_HandleTakesPriority()
    {
        var win = ToolHelpers.FindWindow(Windows, "100");
        Assert.NotNull(win);
        Assert.Equal(100, win.Handle);
    }

    [Fact]
    public void FindWindow_EmptyList_ReturnsNull()
    {
        var win = ToolHelpers.FindWindow(Array.Empty<WindowInfo>(), "anything");
        Assert.Null(win);
    }

    // --- Edge case tests (cycle 228) ---

    [Fact]
    public void FindWindow_NumericTitle_FallsBackToTitleMatch()
    {
        // Handle "999" doesn't match any window handle, so falls back to title substring
        var windows = new[]
        {
            new WindowInfo { Handle = 100, Title = "Window 999", ProcessId = 1, Bounds = new Rectangle(0, 0, 100, 100) },
        };
        var win = ToolHelpers.FindWindow(windows, "999");
        Assert.NotNull(win);
        Assert.Equal(100, win.Handle);
    }

    [Fact]
    public void FindWindow_HandleMatchTakesPriority_OverTitleMatch()
    {
        // "300" matches both handle 300 AND title of first window
        var windows = new[]
        {
            new WindowInfo { Handle = 100, Title = "Port 300 Monitor", ProcessId = 1, Bounds = new Rectangle(0, 0, 100, 100) },
            new WindowInfo { Handle = 300, Title = "Other App", ProcessId = 2, Bounds = new Rectangle(0, 0, 100, 100) },
        };
        var win = ToolHelpers.FindWindow(windows, "300");
        Assert.NotNull(win);
        Assert.Equal(300, win.Handle); // handle match wins
    }

    [Fact]
    public void FindWindow_EmptyTitle_ReturnsNull()
    {
        var win = ToolHelpers.FindWindow(Windows, "");
        // Empty string matches all titles via Contains, returns first
        Assert.NotNull(win);
        Assert.Equal(100, win.Handle);
    }

    [Fact]
    public void FindWindow_PartialTitle_ReturnsFirstMatch()
    {
        // Multiple windows could contain "o" — first match wins
        var win = ToolHelpers.FindWindow(Windows, "Visual");
        Assert.NotNull(win);
        Assert.Equal("Visual Studio Code", win.Title);
    }
}
