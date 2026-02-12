using SharpHook;
using SharpHook.Data;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IActionRecorder"/> using SharpHook for global input hooks.
/// Records mouse and keyboard events, and replays them using <see cref="IMouse"/> and <see cref="IKeyboard"/>.
/// </summary>
public sealed class WindowsActionRecorder : IActionRecorder, IDisposable
{
    private readonly IMouse _mouse;
    private readonly IKeyboard _keyboard;
    private readonly object _lock = new();
    private List<RecordedAction> _actions = [];
    private SimpleGlobalHook? _hook;
    private Task? _hookTask;
    private DateTimeOffset _lastEventTime;
    private bool _recording;
    private int _disposed;

    /// <summary>
    /// Creates a new action recorder that uses the given mouse and keyboard for replay.
    /// </summary>
    public WindowsActionRecorder(IMouse mouse, IKeyboard keyboard)
    {
        _mouse = mouse ?? throw new ArgumentNullException(nameof(mouse));
        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
    }

    /// <inheritdoc />
    public Task StartRecordingAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);

        lock (_lock)
        {
            if (_recording)
                throw new InvalidOperationException("Already recording. Call StopRecordingAsync first.");

            _actions = [];
            _lastEventTime = DateTimeOffset.UtcNow;
            _recording = true;

            _hook = new SimpleGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;
            _hook.MousePressed += OnMousePressed;
            _hook.MouseReleased += OnMouseReleased;
            _hook.MouseMoved += OnMouseMoved;
            _hook.MouseWheel += OnMouseWheel;
        }

        // RunAsync is blocking (runs the hook loop), so start outside lock
        _hookTask = _hook.RunAsync();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopRecordingAsync(CancellationToken ct = default)
    {
        SimpleGlobalHook? hookToDispose;
        Task? hookTaskToAwait;

        lock (_lock)
        {
            if (!_recording)
                return;

            _recording = false;
            hookToDispose = _hook;
            hookTaskToAwait = _hookTask;
            _hook = null;
            _hookTask = null;
        }

        if (hookToDispose is not null)
        {
            hookToDispose.KeyPressed -= OnKeyPressed;
            hookToDispose.KeyReleased -= OnKeyReleased;
            hookToDispose.MousePressed -= OnMousePressed;
            hookToDispose.MouseReleased -= OnMouseReleased;
            hookToDispose.MouseMoved -= OnMouseMoved;
            hookToDispose.MouseWheel -= OnMouseWheel;
            hookToDispose.Dispose();

            if (hookTaskToAwait is not null)
            {
                try { await hookTaskToAwait; }
                catch { /* Hook task may throw on disposal */ }
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RecordedAction>> GetRecordedActionsAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<RecordedAction>>(_actions.ToList());
        }
    }

    /// <inheritdoc />
    public async Task ReplayAsync(
        IReadOnlyList<RecordedAction> actions, double speedMultiplier = 1.0, CancellationToken ct = default)
    {
        if (speedMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "Must be positive.");

        foreach (var action in actions)
        {
            ct.ThrowIfCancellationRequested();

            // Wait for the delay between actions
            if (action.DelayBefore > TimeSpan.Zero)
            {
                var delay = TimeSpan.FromTicks((long)(action.DelayBefore.Ticks / speedMultiplier));
                await Task.Delay(delay, ct);
            }

            switch (action.Type)
            {
                case RecordedActionType.MouseMove when action.X.HasValue && action.Y.HasValue:
                    await _mouse.MoveAsync(action.X.Value, action.Y.Value, ct);
                    break;

                case RecordedActionType.MouseClick when action.X.HasValue && action.Y.HasValue:
                    await _mouse.ClickAsync(action.X.Value, action.Y.Value,
                        action.Button ?? MouseButton.Left, ct);
                    break;

                case RecordedActionType.MouseDown when action.X.HasValue && action.Y.HasValue:
                    await _mouse.ButtonDownAsync(action.X.Value, action.Y.Value,
                        action.Button ?? MouseButton.Left, ct);
                    break;

                case RecordedActionType.MouseUp when action.X.HasValue && action.Y.HasValue:
                    await _mouse.ButtonUpAsync(action.X.Value, action.Y.Value,
                        action.Button ?? MouseButton.Left, ct);
                    break;

                case RecordedActionType.MouseScroll when action.X.HasValue && action.Y.HasValue:
                    await _mouse.ScrollAsync(action.X.Value, action.Y.Value,
                        action.ScrollDelta ?? 0, ct);
                    break;

                case RecordedActionType.KeyPress when action.Key.HasValue:
                    await _keyboard.KeyPressAsync(action.Key.Value, ct);
                    break;

                case RecordedActionType.KeyDown when action.Key.HasValue:
                    await _keyboard.KeyDownAsync(action.Key.Value, ct);
                    break;

                case RecordedActionType.KeyUp when action.Key.HasValue:
                    await _keyboard.KeyUpAsync(action.Key.Value, ct);
                    break;
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        SimpleGlobalHook? hookToDispose;
        Task? hookTaskToAwait;

        lock (_lock)
        {
            _recording = false;
            hookToDispose = _hook;
            hookTaskToAwait = _hookTask;
            _hook = null;
            _hookTask = null;
        }

        hookToDispose?.Dispose();
        try { hookTaskToAwait?.GetAwaiter().GetResult(); }
        catch { /* Hook task may throw on disposal */ }
    }

    /// <summary>
    /// Atomically computes the delay since the last event and records the action.
    /// This ensures event ordering is preserved even under concurrent hook callbacks.
    /// </summary>
    private void RecordWithDelay(Func<TimeSpan, RecordedAction> actionFactory)
    {
        lock (_lock)
        {
            if (!_recording) return;

            var now = DateTimeOffset.UtcNow;
            var delay = now - _lastEventTime;
            _lastEventTime = now;

            _actions.Add(actionFactory(delay));
        }
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var key = MapKeyCode(e.Data.KeyCode);
        if (key is null) return;

        RecordWithDelay(delay => new RecordedAction
        {
            Type = RecordedActionType.KeyDown,
            Timestamp = DateTimeOffset.UtcNow,
            Key = key,
            DelayBefore = delay,
        });
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        var key = MapKeyCode(e.Data.KeyCode);
        if (key is null) return;

        RecordWithDelay(delay => new RecordedAction
        {
            Type = RecordedActionType.KeyUp,
            Timestamp = DateTimeOffset.UtcNow,
            Key = key,
            DelayBefore = delay,
        });
    }

    private void OnMousePressed(object? sender, MouseHookEventArgs e)
    {
        RecordWithDelay(delay => new RecordedAction
        {
            Type = RecordedActionType.MouseDown,
            Timestamp = DateTimeOffset.UtcNow,
            X = e.Data.X,
            Y = e.Data.Y,
            Button = MapMouseButton(e.Data.Button),
            DelayBefore = delay,
        });
    }

    private void OnMouseReleased(object? sender, MouseHookEventArgs e)
    {
        RecordWithDelay(delay => new RecordedAction
        {
            Type = RecordedActionType.MouseUp,
            Timestamp = DateTimeOffset.UtcNow,
            X = e.Data.X,
            Y = e.Data.Y,
            Button = MapMouseButton(e.Data.Button),
            DelayBefore = delay,
        });
    }

    private void OnMouseMoved(object? sender, MouseHookEventArgs e)
    {
        RecordWithDelay(delay => new RecordedAction
        {
            Type = RecordedActionType.MouseMove,
            Timestamp = DateTimeOffset.UtcNow,
            X = e.Data.X,
            Y = e.Data.Y,
            DelayBefore = delay,
        });
    }

    private void OnMouseWheel(object? sender, MouseWheelHookEventArgs e)
    {
        RecordWithDelay(delay => new RecordedAction
        {
            Type = RecordedActionType.MouseScroll,
            Timestamp = DateTimeOffset.UtcNow,
            X = e.Data.X,
            Y = e.Data.Y,
            ScrollDelta = e.Data.Rotation,
            DelayBefore = delay,
        });
    }

    private static MouseButton MapMouseButton(SharpHook.Data.MouseButton button) => button switch
    {
        SharpHook.Data.MouseButton.Button1 => MouseButton.Left,
        SharpHook.Data.MouseButton.Button2 => MouseButton.Right,
        SharpHook.Data.MouseButton.Button3 => MouseButton.Middle,
        _ => MouseButton.Left,
    };

    private static Key? MapKeyCode(KeyCode code) => code switch
    {
        KeyCode.VcLeftControl or KeyCode.VcRightControl => Key.Ctrl,
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt => Key.Alt,
        KeyCode.VcLeftShift or KeyCode.VcRightShift => Key.Shift,
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta => Key.Win,

        KeyCode.VcF1 => Key.F1,
        KeyCode.VcF2 => Key.F2,
        KeyCode.VcF3 => Key.F3,
        KeyCode.VcF4 => Key.F4,
        KeyCode.VcF5 => Key.F5,
        KeyCode.VcF6 => Key.F6,
        KeyCode.VcF7 => Key.F7,
        KeyCode.VcF8 => Key.F8,
        KeyCode.VcF9 => Key.F9,
        KeyCode.VcF10 => Key.F10,
        KeyCode.VcF11 => Key.F11,
        KeyCode.VcF12 => Key.F12,

        KeyCode.VcEscape => Key.Escape,
        KeyCode.VcTab => Key.Tab,
        KeyCode.VcCapsLock => Key.CapsLock,
        KeyCode.VcEnter => Key.Enter,
        KeyCode.VcBackspace => Key.Backspace,
        KeyCode.VcDelete => Key.Delete,
        KeyCode.VcInsert => Key.Insert,
        KeyCode.VcHome => Key.Home,
        KeyCode.VcEnd => Key.End,
        KeyCode.VcPageUp => Key.PageUp,
        KeyCode.VcPageDown => Key.PageDown,
        KeyCode.VcUp => Key.Up,
        KeyCode.VcDown => Key.Down,
        KeyCode.VcLeft => Key.Left,
        KeyCode.VcRight => Key.Right,

        KeyCode.VcSpace => Key.Space,
        KeyCode.VcPrintScreen => Key.PrintScreen,
        KeyCode.VcScrollLock => Key.ScrollLock,
        KeyCode.VcPause => Key.Pause,

        KeyCode.VcA => Key.A,
        KeyCode.VcB => Key.B,
        KeyCode.VcC => Key.C,
        KeyCode.VcD => Key.D,
        KeyCode.VcE => Key.E,
        KeyCode.VcF => Key.F,
        KeyCode.VcG => Key.G,
        KeyCode.VcH => Key.H,
        KeyCode.VcI => Key.I,
        KeyCode.VcJ => Key.J,
        KeyCode.VcK => Key.K,
        KeyCode.VcL => Key.L,
        KeyCode.VcM => Key.M,
        KeyCode.VcN => Key.N,
        KeyCode.VcO => Key.O,
        KeyCode.VcP => Key.P,
        KeyCode.VcQ => Key.Q,
        KeyCode.VcR => Key.R,
        KeyCode.VcS => Key.S,
        KeyCode.VcT => Key.T,
        KeyCode.VcU => Key.U,
        KeyCode.VcV => Key.V,
        KeyCode.VcW => Key.W,
        KeyCode.VcX => Key.X,
        KeyCode.VcY => Key.Y,
        KeyCode.VcZ => Key.Z,

        KeyCode.Vc0 => Key.D0,
        KeyCode.Vc1 => Key.D1,
        KeyCode.Vc2 => Key.D2,
        KeyCode.Vc3 => Key.D3,
        KeyCode.Vc4 => Key.D4,
        KeyCode.Vc5 => Key.D5,
        KeyCode.Vc6 => Key.D6,
        KeyCode.Vc7 => Key.D7,
        KeyCode.Vc8 => Key.D8,
        KeyCode.Vc9 => Key.D9,

        KeyCode.VcNumPad0 => Key.NumPad0,
        KeyCode.VcNumPad1 => Key.NumPad1,
        KeyCode.VcNumPad2 => Key.NumPad2,
        KeyCode.VcNumPad3 => Key.NumPad3,
        KeyCode.VcNumPad4 => Key.NumPad4,
        KeyCode.VcNumPad5 => Key.NumPad5,
        KeyCode.VcNumPad6 => Key.NumPad6,
        KeyCode.VcNumPad7 => Key.NumPad7,
        KeyCode.VcNumPad8 => Key.NumPad8,
        KeyCode.VcNumPad9 => Key.NumPad9,
        KeyCode.VcNumPadMultiply => Key.NumPadMultiply,
        KeyCode.VcNumPadAdd => Key.NumPadAdd,
        KeyCode.VcNumPadSubtract => Key.NumPadSubtract,
        KeyCode.VcNumPadDecimal => Key.NumPadDecimal,
        KeyCode.VcNumPadDivide => Key.NumPadDivide,
        KeyCode.VcNumLock => Key.NumLock,

        _ => null, // Unmapped keys are ignored during recording
    };
}
