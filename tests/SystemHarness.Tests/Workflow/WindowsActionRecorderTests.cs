using SystemHarness.Windows;

namespace SystemHarness.Tests.Workflow;

[Trait("Category", "CI")]
public class WindowsActionRecorderTests
{
    // --- Constructor Tests ---

    [Fact]
    public void Constructor_NullMouse_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsActionRecorder(null!, new StubKeyboard()));
    }

    [Fact]
    public void Constructor_NullKeyboard_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsActionRecorder(new StubMouse(), null!));
    }

    // --- GetRecordedActionsAsync Tests ---

    [Fact]
    public async Task GetRecordedActionsAsync_InitiallyEmpty()
    {
        using var recorder = new WindowsActionRecorder(new StubMouse(), new StubKeyboard());
        var actions = await recorder.GetRecordedActionsAsync();
        Assert.Empty(actions);
    }

    // --- StopRecordingAsync Tests ---

    [Fact]
    public async Task StopRecordingAsync_WhenNotRecording_DoesNotThrow()
    {
        using var recorder = new WindowsActionRecorder(new StubMouse(), new StubKeyboard());
        await recorder.StopRecordingAsync(); // Should not throw
    }

    // --- Dispose Tests ---

    [Fact]
    public void Dispose_DoubleFree_Safe()
    {
        var recorder = new WindowsActionRecorder(new StubMouse(), new StubKeyboard());
        recorder.Dispose();
        recorder.Dispose(); // Should not throw
    }

    [Fact]
    public async Task StartRecordingAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var recorder = new WindowsActionRecorder(new StubMouse(), new StubKeyboard());
        recorder.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => recorder.StartRecordingAsync());
    }

    // --- ReplayAsync Argument Validation ---

    [Fact]
    public async Task ReplayAsync_ZeroSpeedMultiplier_Throws()
    {
        using var recorder = new WindowsActionRecorder(new StubMouse(), new StubKeyboard());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => recorder.ReplayAsync([], speedMultiplier: 0));
    }

    [Fact]
    public async Task ReplayAsync_NegativeSpeedMultiplier_Throws()
    {
        using var recorder = new WindowsActionRecorder(new StubMouse(), new StubKeyboard());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => recorder.ReplayAsync([], speedMultiplier: -1.0));
    }

    [Fact]
    public async Task ReplayAsync_EmptyActions_Completes()
    {
        using var recorder = new WindowsActionRecorder(new StubMouse(), new StubKeyboard());
        await recorder.ReplayAsync([]);
    }

    // --- ReplayAsync Dispatch Tests ---

    [Fact]
    public async Task ReplayAsync_MouseMove_CallsMoveAsync()
    {
        var mouse = new StubMouse();
        using var recorder = new WindowsActionRecorder(mouse, new StubKeyboard());

        await recorder.ReplayAsync([MakeAction(RecordedActionType.MouseMove, x: 100, y: 200)]);

        Assert.Single(mouse.Calls);
        Assert.Equal(("MoveAsync", 100, 200), mouse.Calls[0]);
    }

    [Fact]
    public async Task ReplayAsync_MouseClick_CallsClickAsync()
    {
        var mouse = new StubMouse();
        using var recorder = new WindowsActionRecorder(mouse, new StubKeyboard());

        await recorder.ReplayAsync([MakeAction(RecordedActionType.MouseClick, x: 50, y: 60, button: MouseButton.Right)]);

        Assert.Single(mouse.ClickCalls);
        Assert.Equal((50, 60, MouseButton.Right), mouse.ClickCalls[0]);
    }

    [Fact]
    public async Task ReplayAsync_MouseDown_CallsButtonDownAsync()
    {
        var mouse = new StubMouse();
        using var recorder = new WindowsActionRecorder(mouse, new StubKeyboard());

        await recorder.ReplayAsync([MakeAction(RecordedActionType.MouseDown, x: 10, y: 20, button: MouseButton.Left)]);

        Assert.Single(mouse.ButtonDownCalls);
        Assert.Equal((10, 20, MouseButton.Left), mouse.ButtonDownCalls[0]);
    }

    [Fact]
    public async Task ReplayAsync_MouseUp_CallsButtonUpAsync()
    {
        var mouse = new StubMouse();
        using var recorder = new WindowsActionRecorder(mouse, new StubKeyboard());

        await recorder.ReplayAsync([MakeAction(RecordedActionType.MouseUp, x: 30, y: 40, button: MouseButton.Middle)]);

        Assert.Single(mouse.ButtonUpCalls);
        Assert.Equal((30, 40, MouseButton.Middle), mouse.ButtonUpCalls[0]);
    }

    [Fact]
    public async Task ReplayAsync_MouseScroll_CallsScrollAsync()
    {
        var mouse = new StubMouse();
        using var recorder = new WindowsActionRecorder(mouse, new StubKeyboard());

        await recorder.ReplayAsync([MakeAction(RecordedActionType.MouseScroll, x: 5, y: 10, scrollDelta: -3)]);

        Assert.Single(mouse.ScrollCalls);
        Assert.Equal((5, 10, -3), mouse.ScrollCalls[0]);
    }

    [Fact]
    public async Task ReplayAsync_KeyPress_CallsKeyPressAsync()
    {
        var keyboard = new StubKeyboard();
        using var recorder = new WindowsActionRecorder(new StubMouse(), keyboard);

        await recorder.ReplayAsync([MakeAction(RecordedActionType.KeyPress, key: Key.Enter)]);

        Assert.Single(keyboard.KeyPressCalls);
        Assert.Equal(Key.Enter, keyboard.KeyPressCalls[0]);
    }

    [Fact]
    public async Task ReplayAsync_KeyDown_CallsKeyDownAsync()
    {
        var keyboard = new StubKeyboard();
        using var recorder = new WindowsActionRecorder(new StubMouse(), keyboard);

        await recorder.ReplayAsync([MakeAction(RecordedActionType.KeyDown, key: Key.Ctrl)]);

        Assert.Single(keyboard.KeyDownCalls);
        Assert.Equal(Key.Ctrl, keyboard.KeyDownCalls[0]);
    }

    [Fact]
    public async Task ReplayAsync_KeyUp_CallsKeyUpAsync()
    {
        var keyboard = new StubKeyboard();
        using var recorder = new WindowsActionRecorder(new StubMouse(), keyboard);

        await recorder.ReplayAsync([MakeAction(RecordedActionType.KeyUp, key: Key.Shift)]);

        Assert.Single(keyboard.KeyUpCalls);
        Assert.Equal(Key.Shift, keyboard.KeyUpCalls[0]);
    }

    [Fact]
    public async Task ReplayAsync_MultipleActions_DispatchesAll()
    {
        var mouse = new StubMouse();
        var keyboard = new StubKeyboard();
        using var recorder = new WindowsActionRecorder(mouse, keyboard);

        await recorder.ReplayAsync([
            MakeAction(RecordedActionType.MouseMove, x: 100, y: 100),
            MakeAction(RecordedActionType.MouseClick, x: 100, y: 100),
            MakeAction(RecordedActionType.KeyPress, key: Key.A),
        ]);

        Assert.Single(mouse.Calls);
        Assert.Single(mouse.ClickCalls);
        Assert.Single(keyboard.KeyPressCalls);
    }

    [Fact]
    public async Task ReplayAsync_MouseActionWithoutCoordinates_Skipped()
    {
        var mouse = new StubMouse();
        using var recorder = new WindowsActionRecorder(mouse, new StubKeyboard());

        // MouseMove without X/Y should be silently skipped (pattern guard fails)
        await recorder.ReplayAsync([new RecordedAction
        {
            Type = RecordedActionType.MouseMove,
            Timestamp = DateTimeOffset.UtcNow,
        }]);

        Assert.Empty(mouse.Calls);
    }

    [Fact]
    public async Task ReplayAsync_KeyActionWithoutKey_Skipped()
    {
        var keyboard = new StubKeyboard();
        using var recorder = new WindowsActionRecorder(new StubMouse(), keyboard);

        // KeyPress without Key should be silently skipped (pattern guard fails)
        await recorder.ReplayAsync([new RecordedAction
        {
            Type = RecordedActionType.KeyPress,
            Timestamp = DateTimeOffset.UtcNow,
        }]);

        Assert.Empty(keyboard.KeyPressCalls);
    }

    [Fact]
    public async Task ReplayAsync_Cancellation_StopsEarly()
    {
        var mouse = new StubMouse();
        using var recorder = new WindowsActionRecorder(mouse, new StubKeyboard());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            recorder.ReplayAsync([MakeAction(RecordedActionType.MouseMove, x: 1, y: 1)], ct: cts.Token));

        Assert.Empty(mouse.Calls);
    }

    [Fact]
    public async Task ReplayAsync_ClickDefaultsToLeftButton()
    {
        var mouse = new StubMouse();
        using var recorder = new WindowsActionRecorder(mouse, new StubKeyboard());

        // MouseClick without Button specified → defaults to Left
        await recorder.ReplayAsync([new RecordedAction
        {
            Type = RecordedActionType.MouseClick,
            Timestamp = DateTimeOffset.UtcNow,
            X = 10,
            Y = 20,
            // Button is null
        }]);

        Assert.Single(mouse.ClickCalls);
        Assert.Equal(MouseButton.Left, mouse.ClickCalls[0].Button);
    }

    // --- Helpers ---

    private static RecordedAction MakeAction(
        RecordedActionType type,
        int? x = null, int? y = null,
        MouseButton? button = null,
        Key? key = null,
        int? scrollDelta = null) => new()
    {
        Type = type,
        Timestamp = DateTimeOffset.UtcNow,
        X = x,
        Y = y,
        Button = button,
        Key = key,
        ScrollDelta = scrollDelta,
        DelayBefore = TimeSpan.Zero,
    };

    // --- Stubs ---

    private class StubMouse : IMouse
    {
        public List<(string Method, int X, int Y)> Calls { get; } = [];
        public List<(int X, int Y, MouseButton Button)> ClickCalls { get; } = [];
        public List<(int X, int Y, MouseButton Button)> ButtonDownCalls { get; } = [];
        public List<(int X, int Y, MouseButton Button)> ButtonUpCalls { get; } = [];
        public List<(int X, int Y, int Delta)> ScrollCalls { get; } = [];

        public Task ClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            ClickCalls.Add((x, y, button));
            return Task.CompletedTask;
        }

        public Task DoubleClickAsync(int x, int y, CancellationToken ct = default) => Task.CompletedTask;
        public Task RightClickAsync(int x, int y, CancellationToken ct = default) => Task.CompletedTask;
        public Task DragAsync(int fromX, int fromY, int toX, int toY, CancellationToken ct = default) => Task.CompletedTask;

        public Task ScrollAsync(int x, int y, int delta, CancellationToken ct = default)
        {
            ScrollCalls.Add((x, y, delta));
            return Task.CompletedTask;
        }

        public Task MoveAsync(int x, int y, CancellationToken ct = default)
        {
            Calls.Add(("MoveAsync", x, y));
            return Task.CompletedTask;
        }

        public Task<(int X, int Y)> GetPositionAsync(CancellationToken ct = default) =>
            Task.FromResult((0, 0));

        public Task ButtonDownAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            ButtonDownCalls.Add((x, y, button));
            return Task.CompletedTask;
        }

        public Task ButtonUpAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            ButtonUpCalls.Add((x, y, button));
            return Task.CompletedTask;
        }
    }

    private class StubKeyboard : IKeyboard
    {
        public List<Key> KeyPressCalls { get; } = [];
        public List<Key> KeyDownCalls { get; } = [];
        public List<Key> KeyUpCalls { get; } = [];

        public Task TypeAsync(string text, int delayMs = 0, CancellationToken ct = default) => Task.CompletedTask;

        public Task KeyPressAsync(Key key, CancellationToken ct = default)
        {
            KeyPressCalls.Add(key);
            return Task.CompletedTask;
        }

        public Task KeyDownAsync(Key key, CancellationToken ct = default)
        {
            KeyDownCalls.Add(key);
            return Task.CompletedTask;
        }

        public Task KeyUpAsync(Key key, CancellationToken ct = default)
        {
            KeyUpCalls.Add(key);
            return Task.CompletedTask;
        }

        public Task HotkeyAsync(CancellationToken ct = default, params Key[] keys) => Task.CompletedTask;
    }
}
