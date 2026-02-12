using SystemHarness.Windows;

namespace SystemHarness.Tests.Window;

[Trait("Category", "CI")]
public class DialogHandlerTests
{
    // --- Stub ---

    private sealed class StubKeyboard : IKeyboard
    {
        public List<(string Method, Key[] Keys)> Calls { get; } = [];
        public List<string> TypedTexts { get; } = [];

        public Task TypeAsync(string text, int delayMs = 0, CancellationToken ct = default)
        {
            TypedTexts.Add(text);
            return Task.CompletedTask;
        }

        public Task KeyPressAsync(Key key, CancellationToken ct = default)
        {
            Calls.Add(("KeyPress", [key]));
            return Task.CompletedTask;
        }

        public Task KeyDownAsync(Key key, CancellationToken ct = default)
        {
            Calls.Add(("KeyDown", [key]));
            return Task.CompletedTask;
        }

        public Task KeyUpAsync(Key key, CancellationToken ct = default)
        {
            Calls.Add(("KeyUp", [key]));
            return Task.CompletedTask;
        }

        public Task HotkeyAsync(CancellationToken ct = default, params Key[] keys)
        {
            Calls.Add(("Hotkey", keys));
            return Task.CompletedTask;
        }
    }

    // --- ClickDialogButtonAsync branch tests ---

    [Fact]
    public async Task ClickDialogButton_OK_PressesEnter()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("OK");

        Assert.Single(kb.Calls);
        Assert.Equal("KeyPress", kb.Calls[0].Method);
        Assert.Equal(Key.Enter, kb.Calls[0].Keys[0]);
    }

    [Fact]
    public async Task ClickDialogButton_Cancel_PressesEscape()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("Cancel");

        Assert.Single(kb.Calls);
        Assert.Equal("KeyPress", kb.Calls[0].Method);
        Assert.Equal(Key.Escape, kb.Calls[0].Keys[0]);
    }

    [Fact]
    public async Task ClickDialogButton_Yes_PressesAltY()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("Yes");

        Assert.Single(kb.Calls);
        Assert.Equal("Hotkey", kb.Calls[0].Method);
        Assert.Equal([Key.Alt, Key.Y], kb.Calls[0].Keys);
    }

    [Fact]
    public async Task ClickDialogButton_No_PressesAltN()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("No");

        Assert.Single(kb.Calls);
        Assert.Equal("Hotkey", kb.Calls[0].Method);
        Assert.Equal([Key.Alt, Key.N], kb.Calls[0].Keys);
    }

    [Fact]
    public async Task ClickDialogButton_Save_PressesAltS()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("Save");

        Assert.Single(kb.Calls);
        Assert.Equal("Hotkey", kb.Calls[0].Method);
        Assert.Equal([Key.Alt, Key.S], kb.Calls[0].Keys);
    }

    [Fact]
    public async Task ClickDialogButton_DontSave_PressesAltN()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("Don't Save");

        Assert.Single(kb.Calls);
        Assert.Equal("Hotkey", kb.Calls[0].Method);
        Assert.Equal([Key.Alt, Key.N], kb.Calls[0].Keys);
    }

    [Fact]
    public async Task ClickDialogButton_DontSaveNoApostrophe_PressesAltN()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("Dont Save");

        Assert.Single(kb.Calls);
        Assert.Equal("Hotkey", kb.Calls[0].Method);
        Assert.Equal([Key.Alt, Key.N], kb.Calls[0].Keys);
    }

    [Fact]
    public async Task ClickDialogButton_CaseInsensitive()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("ok");

        Assert.Single(kb.Calls);
        Assert.Equal("KeyPress", kb.Calls[0].Method);
        Assert.Equal(Key.Enter, kb.Calls[0].Keys[0]);
    }

    [Fact]
    public async Task ClickDialogButton_UnknownButton_TabsAndEnter()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.ClickDialogButtonAsync("Custom Button");

        // 10 Tab presses + 1 Enter
        Assert.Equal(11, kb.Calls.Count);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal("KeyPress", kb.Calls[i].Method);
            Assert.Equal(Key.Tab, kb.Calls[i].Keys[0]);
        }
        Assert.Equal("KeyPress", kb.Calls[10].Method);
        Assert.Equal(Key.Enter, kb.Calls[10].Keys[0]);
    }

    // --- DismissMessageBoxAsync tests ---

    [Fact]
    public async Task DismissMessageBox_NullButton_PressesEscape()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.DismissMessageBoxAsync(null);

        Assert.Single(kb.Calls);
        Assert.Equal("KeyPress", kb.Calls[0].Method);
        Assert.Equal(Key.Escape, kb.Calls[0].Keys[0]);
    }

    [Fact]
    public async Task DismissMessageBox_WithButton_DelegatesToClickButton()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.DismissMessageBoxAsync("Yes");

        Assert.Single(kb.Calls);
        Assert.Equal("Hotkey", kb.Calls[0].Method);
        Assert.Equal([Key.Alt, Key.Y], kb.Calls[0].Keys);
    }

    // --- SetFileDialogPathAsync tests ---

    [Fact]
    public async Task SetFileDialogPath_TypesPathAndPressesEnter()
    {
        var kb = new StubKeyboard();
        var handler = new WindowsDialogHandler(kb);

        await handler.SetFileDialogPathAsync(@"C:\test\file.txt");

        Assert.Single(kb.TypedTexts);
        Assert.Equal(@"C:\test\file.txt", kb.TypedTexts[0]);
        Assert.Single(kb.Calls);
        Assert.Equal("KeyPress", kb.Calls[0].Method);
        Assert.Equal(Key.Enter, kb.Calls[0].Keys[0]);
    }

    // --- IsDialogOpenAsync tests (Win32, no stub) ---

    [Fact]
    public async Task IsDialogOpen_WithNonExistentParent_ReturnsFalse()
    {
        var handler = new WindowsDialogHandler();

        var result = await handler.IsDialogOpenAsync("NonExistentWindow_XYZ_67890");

        Assert.False(result);
    }

    [Fact]
    public async Task IsDialogOpen_SupportsCancellation()
    {
        var handler = new WindowsDialogHandler();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.IsDialogOpenAsync("Test", cts.Token));
    }
}
