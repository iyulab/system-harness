using System.Globalization;
using ChildProcessGuard;
using SystemHarness.Windows;

namespace SystemHarness.Tests.Window;

public class NotepadFixture : IAsyncLifetime
{
    public ProcessGuardian Guardian { get; } = new();
    public int NotepadPid { get; private set; }

    public async Task InitializeAsync()
    {
        var proc = Guardian.StartProcess("notepad.exe");
        NotepadPid = proc.Id;
        await Task.Delay(1500);
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
[Trait("Category", "RequiresDesktop")]
public class WindowsWindowTests : IClassFixture<NotepadFixture>
{
    private readonly WindowsWindow _window = new();
    private readonly ProcessGuardian _guardian;

    public WindowsWindowTests(NotepadFixture notepadFixture, DesktopInteractionFixture collectionFixture)
    {
        // Use collection-level guardian for ad-hoc processes (e.g. calc.exe)
        _guardian = collectionFixture.Guardian;
    }

    [Fact]
    public async Task ListAsync_ReturnsVisibleWindows()
    {
        var windows = await _window.ListAsync();

        Assert.NotEmpty(windows);
        Assert.All(windows, w =>
        {
            Assert.NotNull(w.Title);
            Assert.NotEmpty(w.Title);
            Assert.True(w.IsVisible);
            Assert.True(w.Handle != 0);
        });
    }

    [Fact]
    public async Task ListAsync_WindowsHaveBoundsAndProcessId()
    {
        var windows = await _window.ListAsync();

        Assert.Contains(windows, w =>
            w.Bounds.Width > 0 && w.Bounds.Height > 0);
        Assert.All(windows, w => Assert.True(w.ProcessId >= 0));
    }

    [Fact]
    public async Task FocusAsync_WithNotepad_Works()
    {
        await _window.FocusAsync("Notepad");

        var windows = await _window.ListAsync();
        Assert.Contains(windows, w =>
            w.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MinimizeAsync_AndMaximizeAsync_Work()
    {
        await _window.MinimizeAsync("Notepad");
        await Task.Delay(300);

        await _window.MaximizeAsync("Notepad");
        await Task.Delay(300);

        var windows = await _window.ListAsync();
        Assert.Contains(windows, w =>
            w.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResizeAsync_ChangesWindowSize()
    {
        await _window.FocusAsync("Notepad");
        await Task.Delay(200);

        await _window.ResizeAsync("Notepad", 800, 600);
        await Task.Delay(300);

        var windows = await _window.ListAsync();
        var notepad = windows.FirstOrDefault(w =>
            w.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(notepad);
        Assert.InRange(notepad.Bounds.Width, 780, 830);
        Assert.InRange(notepad.Bounds.Height, 580, 630);
    }

    [Fact]
    public async Task MoveAsync_ChangesWindowPosition()
    {
        await _window.MoveAsync("Notepad", 100, 100);
        await Task.Delay(300);

        var windows = await _window.ListAsync();
        var notepad = windows.FirstOrDefault(w =>
            w.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(notepad);
        Assert.Equal(100, notepad.Bounds.X);
        Assert.Equal(100, notepad.Bounds.Y);
    }

    [Fact]
    public async Task FocusAsync_NonExistentWindow_ThrowsHarnessException()
    {
        await Assert.ThrowsAsync<HarnessException>(() =>
            _window.FocusAsync("NonExistentWindow_XYZ_99999"));
    }

    [Fact]
    public async Task FocusAsync_ByHandle_Works()
    {
        var windows = await _window.ListAsync();
        var first = windows[0];

        await _window.FocusAsync(first.Handle.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task CloseAsync_ClosesWindow()
    {
        _guardian.StartProcess("calc.exe");
        await Task.Delay(1500);

        var windows = await _window.ListAsync();
        var target = windows.FirstOrDefault(w =>
            w.Title.Contains("Calc", StringComparison.OrdinalIgnoreCase) ||
            w.Title.Contains("계산기", StringComparison.OrdinalIgnoreCase) ||
            w.Title.Contains("Calculator", StringComparison.OrdinalIgnoreCase));

        if (target is not null)
        {
            await _window.CloseAsync(target.Handle.ToString(CultureInfo.InvariantCulture));
            await Task.Delay(500);
        }
    }
}
