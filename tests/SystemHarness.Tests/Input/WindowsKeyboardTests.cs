using ChildProcessGuard;
using SystemHarness.Windows;

namespace SystemHarness.Tests.Input;

public class KeyboardNotepadFixture : IAsyncLifetime
{
    public ProcessGuardian Guardian { get; } = new();
    public int NotepadPid { get; private set; }

    public async ValueTask InitializeAsync()
    {
        var proc = Guardian.StartProcess("notepad.exe");
        NotepadPid = proc.Id;
        await Task.Delay(1500);
    }

    public async ValueTask DisposeAsync()
    {
        await NotepadHelper.CloseNotepadByPidAsync(NotepadPid);
        await Guardian.KillAllProcessesAsync();
        Guardian.Dispose();
        GC.SuppressFinalize(this);
    }
}

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
[Trait("Category", "RequiresDesktop")]
public class WindowsKeyboardTests : IClassFixture<KeyboardNotepadFixture>
{
    private readonly WindowsKeyboard _keyboard = new();
    private readonly WindowsClipboard _clipboard = new();
    private readonly WindowsWindow _window = new();

    public WindowsKeyboardTests(KeyboardNotepadFixture _, DesktopInteractionFixture __) { }

    private async Task FocusAndClearNotepad()
    {
        await _window.FocusAsync("Notepad");
        await Task.Delay(200);
        await _keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
        await Task.Delay(50);
        await _keyboard.KeyPressAsync(Key.Delete);
        await Task.Delay(100);
    }

    private async Task<string?> GetNotepadText()
    {
        await _keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
        await Task.Delay(100);
        await _keyboard.HotkeyAsync(default, Key.Ctrl, Key.C);
        await Task.Delay(200);
        return await _clipboard.GetTextAsync();
    }

    [Fact]
    public async Task TypeAsync_TypesText()
    {
        await FocusAndClearNotepad();
        await _keyboard.TypeAsync("hello", ct: TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var text = await GetNotepadText();
        Assert.NotNull(text);
        Assert.Contains("hello", text);
    }

    [Fact]
    public async Task TypeAsync_WithDelay_TypesSlowly()
    {
        await FocusAndClearNotepad();
        var start = DateTime.UtcNow;
        await _keyboard.TypeAsync("abc", delayMs: 50, ct: TestContext.Current.CancellationToken);
        var elapsed = DateTime.UtcNow - start;

        Assert.True(elapsed.TotalMilliseconds >= 90);
    }

    [Fact]
    public async Task KeyPressAsync_Enter_CreatesNewLine()
    {
        await FocusAndClearNotepad();
        await _keyboard.TypeAsync("line1", ct: TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await _keyboard.KeyPressAsync(Key.Enter, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        await _keyboard.TypeAsync("line2", ct: TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var text = await GetNotepadText();
        Assert.NotNull(text);
        Assert.Contains("line1", text);
        Assert.Contains("line2", text);
    }

    [Fact]
    public async Task HotkeyAsync_CtrlA_SelectsAll()
    {
        await FocusAndClearNotepad();
        await _keyboard.TypeAsync("test text", ct: TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        // Re-focus to guard against focus loss between typing and hotkey
        await _window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        await _keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.A });
        await Task.Delay(200, TestContext.Current.CancellationToken);
        await _keyboard.TypeAsync("replaced", ct: TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        var text = await GetNotepadText();
        Assert.NotNull(text);
        Assert.Contains("replaced", text);
    }

    [Fact]
    public async Task KeyDownAsync_AndKeyUpAsync_Work()
    {
        await FocusAndClearNotepad();
        await _keyboard.KeyDownAsync(Key.Shift, TestContext.Current.CancellationToken);
        await _keyboard.KeyPressAsync(Key.A, TestContext.Current.CancellationToken);
        await _keyboard.KeyUpAsync(Key.Shift, TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var text = await GetNotepadText();
        Assert.NotNull(text);
        Assert.Contains("A", text);
    }

    [Fact]
    public async Task TypeAsync_SpecialCharacters_TypesCorrectly()
    {
        await FocusAndClearNotepad();
        await _keyboard.TypeAsync("@#$%^&*()", ct: TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var text = await GetNotepadText();
        Assert.NotNull(text);
        Assert.Contains("@#$%^&*()", text);
    }

    [Fact]
    public async Task TypeAsync_Unicode_TypesBmpCharacters()
    {
        await FocusAndClearNotepad();
        // CJK and accented characters (all BMP)
        await _keyboard.TypeAsync("\u00e9\u00f1\u00fc", ct: TestContext.Current.CancellationToken);  // éñü
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var text = await GetNotepadText();
        Assert.NotNull(text);
        Assert.Contains("\u00e9", text);  // é
    }

    [Fact]
    public async Task TypeAsync_LongText_UsesClipboardFallback()
    {
        await FocusAndClearNotepad();
        // Generate text longer than ClipboardThreshold (512)
        var longText = new string('x', 600);
        await _keyboard.TypeAsync(longText, ct: TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        var text = await GetNotepadText();
        Assert.NotNull(text);
        Assert.Contains(new string('x', 100), text); // Spot check
    }

    [Fact]
    public async Task TypeAsync_BatchMode_SendsAtomically()
    {
        await FocusAndClearNotepad();
        // Short text without delay uses batch SendInput
        await _keyboard.TypeAsync("batch123", ct: TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var text = await GetNotepadText();
        Assert.NotNull(text);
        Assert.Contains("batch123", text);
    }

    [Fact]
    public async Task TypeAsync_EmptyString_DoesNotThrow()
    {
        await FocusAndClearNotepad();
        await _keyboard.TypeAsync("", ct: TestContext.Current.CancellationToken);
        await _keyboard.TypeAsync(string.Empty, ct: TestContext.Current.CancellationToken);
        // No assert needed — just verify no exception
    }
}
