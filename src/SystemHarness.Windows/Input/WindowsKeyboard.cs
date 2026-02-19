using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IKeyboard"/> using SendInput API.
/// Handles Unicode surrogates atomically and uses clipboard paste for long text.
/// </summary>
public sealed class WindowsKeyboard : IKeyboard
{
    /// <summary>
    /// Character count threshold above which clipboard paste (Ctrl+V) is used
    /// instead of per-character SendInput for performance.
    /// </summary>
    private const int ClipboardThreshold = 512;

    private readonly WindowsClipboard _clipboard = new();

    public Task TypeAsync(string text, int delayMs = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
            return Task.CompletedTask;

        // Clipboard paste for long text (fast, atomic)
        if (delayMs == 0 && text.Length > ClipboardThreshold)
            return TypeViaClipboardAsync(text, ct);

        // Per-character SendInput with surrogate pair support
        return TypeViaSendInputAsync(text, delayMs, ct);
    }

    public Task KeyPressAsync(Key key, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var vk = MapKeyToVk(key);
            SendKeyDown(vk);
            SendKeyUp(vk);
        }, ct);
    }

    public Task KeyDownAsync(Key key, CancellationToken ct = default)
    {
        return Task.Run(() => SendKeyDown(MapKeyToVk(key)), ct);
    }

    public Task KeyUpAsync(Key key, CancellationToken ct = default)
    {
        return Task.Run(() => SendKeyUp(MapKeyToVk(key)), ct);
    }

    public Task HotkeyAsync(CancellationToken ct = default, params Key[] keys)
    {
        return Task.Run(() =>
        {
            if (keys.Length == 0) return;

            // Hold all modifier keys down, then press the last key
            var modifiers = keys.AsSpan(0, keys.Length - 1);
            var mainKey = keys[^1];

            foreach (var mod in modifiers)
                SendKeyDown(MapKeyToVk(mod));

            var vk = MapKeyToVk(mainKey);
            SendKeyDown(vk);
            SendKeyUp(vk);

            // Release modifiers in reverse order
            for (var i = modifiers.Length - 1; i >= 0; i--)
                SendKeyUp(MapKeyToVk(modifiers[i]));
        }, ct);
    }

    private static Task TypeViaSendInputAsync(string text, int delayMs, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            if (delayMs > 0)
            {
                // Slow mode: send each character individually with delay
                for (var i = 0; i < text.Length; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        SendSurrogatePair(text[i], text[i + 1]);
                        i++; // skip low surrogate
                    }
                    else
                    {
                        SendUnicodeChar(text[i]);
                    }
                    await Task.Delay(delayMs, ct);
                }
            }
            else
            {
                // Fast mode: batch all characters into a single SendInput call
                SendUnicodeString(text);
            }
        }, ct);
    }

    private async Task TypeViaClipboardAsync(string text, CancellationToken ct)
    {
        // Save current clipboard contents
        var savedText = await _clipboard.GetTextAsync(ct);

        try
        {
            await _clipboard.SetTextAsync(text, ct);
            await Task.Delay(50, ct); // Allow clipboard propagation

            // Send Ctrl+V atomically
            await HotkeyAsync(ct, Key.Ctrl, Key.V);
            await Task.Delay(50, ct); // Allow paste to complete
        }
        finally
        {
            // Restore original clipboard (best effort)
            try
            {
                if (savedText is not null)
                    await _clipboard.SetTextAsync(savedText, ct);
            }
            catch
            {
                // Clipboard restore failure is non-critical
            }
        }
    }

    /// <summary>
    /// Sends an entire string as a batch of Unicode INPUT events in a single SendInput call.
    /// Properly handles surrogate pairs by sending high+low surrogates together.
    /// </summary>
    private static void SendUnicodeString(string text)
    {
        // Calculate INPUT count: BMP chars need 2 (down+up), surrogates need 4
        var inputCount = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                inputCount += 4; // high-down, low-down, high-up, low-up
                i++;
            }
            else
            {
                inputCount += 2; // down, up
            }
        }

        var inputs = new INPUT[inputCount];
        var idx = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                var high = text[i];
                var low = text[i + 1];
                inputs[idx++] = MakeUnicodeInput(high, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE);
                inputs[idx++] = MakeUnicodeInput(low, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE);
                inputs[idx++] = MakeUnicodeInput(high, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
                inputs[idx++] = MakeUnicodeInput(low, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
                i++;
            }
            else
            {
                inputs[idx++] = MakeUnicodeInput(text[i], KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE);
                inputs[idx++] = MakeUnicodeInput(text[i], KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
            }
        }

        SendInputBatch(inputs);
    }

    /// <summary>
    /// Sends a surrogate pair as 4 atomic INPUT events.
    /// </summary>
    private static void SendSurrogatePair(char high, char low)
    {
        var inputs = new INPUT[4];
        inputs[0] = MakeUnicodeInput(high, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE);
        inputs[1] = MakeUnicodeInput(low, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE);
        inputs[2] = MakeUnicodeInput(high, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
        inputs[3] = MakeUnicodeInput(low, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
        SendInputBatch(inputs);
    }

    private static void SendUnicodeChar(char ch)
    {
        var inputs = new INPUT[2];
        inputs[0] = MakeUnicodeInput(ch, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE);
        inputs[1] = MakeUnicodeInput(ch, KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
        SendInputBatch(inputs);
    }

    private static INPUT MakeUnicodeInput(char scanCode, KEYBD_EVENT_FLAGS flags)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki = new KEYBDINPUT
        {
            wVk = 0,
            wScan = scanCode,
            dwFlags = flags,
        };
        return input;
    }

    private static void SendKeyDown(VIRTUAL_KEY vk)
    {
        SendKeyInput(vk, 0, 0);
    }

    private static void SendKeyUp(VIRTUAL_KEY vk)
    {
        SendKeyInput(vk, 0, KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
    }

    private static void SendKeyInput(VIRTUAL_KEY vk, ushort scan, KEYBD_EVENT_FLAGS flags)
    {
        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki = new KEYBDINPUT
        {
            wVk = vk,
            wScan = scan,
            dwFlags = flags,
        };

        unsafe
        {
            PInvoke.SendInput(new ReadOnlySpan<INPUT>(&input, 1), sizeof(INPUT));
        }
    }

    private static void SendInputBatch(INPUT[] inputs)
    {
        unsafe
        {
            fixed (INPUT* ptr = inputs)
            {
                PInvoke.SendInput(new ReadOnlySpan<INPUT>(ptr, inputs.Length), sizeof(INPUT));
            }
        }
    }

    // --- Phase 9 Extensions ---

    public Task<bool> IsKeyPressedAsync(Key key, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var vk = MapKeyToVk(key);
            var state = PInvoke.GetAsyncKeyState((int)vk);
            // High bit set means key is currently down
            return (state & 0x8000) != 0;
        }, ct);
    }

    public Task ToggleKeyAsync(Key key, bool state, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var vk = MapKeyToVk(key);

            // Only toggle keys support this: CapsLock, NumLock, ScrollLock
            if (key != Key.CapsLock && key != Key.NumLock && key != Key.ScrollLock)
                throw new HarnessException($"ToggleKeyAsync only supports CapsLock, NumLock, ScrollLock. Got: {key}");

            // GetKeyState: low bit = toggled state
            var currentState = (PInvoke.GetKeyState((int)vk) & 1) != 0;

            if (currentState != state)
            {
                // Press and release the key to toggle it
                SendKeyDown(vk);
                SendKeyUp(vk);
            }
        }, ct);
    }

    private static VIRTUAL_KEY MapKeyToVk(Key key) => key switch
    {
        // Modifiers
        Key.Ctrl => VIRTUAL_KEY.VK_CONTROL,
        Key.Alt => VIRTUAL_KEY.VK_MENU,
        Key.Shift => VIRTUAL_KEY.VK_SHIFT,
        Key.Win => VIRTUAL_KEY.VK_LWIN,

        // Function keys
        Key.F1 => VIRTUAL_KEY.VK_F1,
        Key.F2 => VIRTUAL_KEY.VK_F2,
        Key.F3 => VIRTUAL_KEY.VK_F3,
        Key.F4 => VIRTUAL_KEY.VK_F4,
        Key.F5 => VIRTUAL_KEY.VK_F5,
        Key.F6 => VIRTUAL_KEY.VK_F6,
        Key.F7 => VIRTUAL_KEY.VK_F7,
        Key.F8 => VIRTUAL_KEY.VK_F8,
        Key.F9 => VIRTUAL_KEY.VK_F9,
        Key.F10 => VIRTUAL_KEY.VK_F10,
        Key.F11 => VIRTUAL_KEY.VK_F11,
        Key.F12 => VIRTUAL_KEY.VK_F12,

        // Navigation
        Key.Escape => VIRTUAL_KEY.VK_ESCAPE,
        Key.Tab => VIRTUAL_KEY.VK_TAB,
        Key.CapsLock => VIRTUAL_KEY.VK_CAPITAL,
        Key.Enter => VIRTUAL_KEY.VK_RETURN,
        Key.Backspace => VIRTUAL_KEY.VK_BACK,
        Key.Delete => VIRTUAL_KEY.VK_DELETE,
        Key.Insert => VIRTUAL_KEY.VK_INSERT,
        Key.Home => VIRTUAL_KEY.VK_HOME,
        Key.End => VIRTUAL_KEY.VK_END,
        Key.PageUp => VIRTUAL_KEY.VK_PRIOR,
        Key.PageDown => VIRTUAL_KEY.VK_NEXT,
        Key.Up => VIRTUAL_KEY.VK_UP,
        Key.Down => VIRTUAL_KEY.VK_DOWN,
        Key.Left => VIRTUAL_KEY.VK_LEFT,
        Key.Right => VIRTUAL_KEY.VK_RIGHT,

        // Special
        Key.Space => VIRTUAL_KEY.VK_SPACE,
        Key.PrintScreen => VIRTUAL_KEY.VK_SNAPSHOT,
        Key.ScrollLock => VIRTUAL_KEY.VK_SCROLL,
        Key.Pause => VIRTUAL_KEY.VK_PAUSE,
        Key.Menu => VIRTUAL_KEY.VK_APPS,

        // Letters (A-Z = 0x41-0x5A)
        Key.A => (VIRTUAL_KEY)0x41,
        Key.B => (VIRTUAL_KEY)0x42,
        Key.C => (VIRTUAL_KEY)0x43,
        Key.D => (VIRTUAL_KEY)0x44,
        Key.E => (VIRTUAL_KEY)0x45,
        Key.F => (VIRTUAL_KEY)0x46,
        Key.G => (VIRTUAL_KEY)0x47,
        Key.H => (VIRTUAL_KEY)0x48,
        Key.I => (VIRTUAL_KEY)0x49,
        Key.J => (VIRTUAL_KEY)0x4A,
        Key.K => (VIRTUAL_KEY)0x4B,
        Key.L => (VIRTUAL_KEY)0x4C,
        Key.M => (VIRTUAL_KEY)0x4D,
        Key.N => (VIRTUAL_KEY)0x4E,
        Key.O => (VIRTUAL_KEY)0x4F,
        Key.P => (VIRTUAL_KEY)0x50,
        Key.Q => (VIRTUAL_KEY)0x51,
        Key.R => (VIRTUAL_KEY)0x52,
        Key.S => (VIRTUAL_KEY)0x53,
        Key.T => (VIRTUAL_KEY)0x54,
        Key.U => (VIRTUAL_KEY)0x55,
        Key.V => (VIRTUAL_KEY)0x56,
        Key.W => (VIRTUAL_KEY)0x57,
        Key.X => (VIRTUAL_KEY)0x58,
        Key.Y => (VIRTUAL_KEY)0x59,
        Key.Z => (VIRTUAL_KEY)0x5A,

        // Numbers (0-9 = 0x30-0x39)
        Key.D0 => (VIRTUAL_KEY)0x30,
        Key.D1 => (VIRTUAL_KEY)0x31,
        Key.D2 => (VIRTUAL_KEY)0x32,
        Key.D3 => (VIRTUAL_KEY)0x33,
        Key.D4 => (VIRTUAL_KEY)0x34,
        Key.D5 => (VIRTUAL_KEY)0x35,
        Key.D6 => (VIRTUAL_KEY)0x36,
        Key.D7 => (VIRTUAL_KEY)0x37,
        Key.D8 => (VIRTUAL_KEY)0x38,
        Key.D9 => (VIRTUAL_KEY)0x39,

        // Numpad
        Key.NumPad0 => VIRTUAL_KEY.VK_NUMPAD0,
        Key.NumPad1 => VIRTUAL_KEY.VK_NUMPAD1,
        Key.NumPad2 => VIRTUAL_KEY.VK_NUMPAD2,
        Key.NumPad3 => VIRTUAL_KEY.VK_NUMPAD3,
        Key.NumPad4 => VIRTUAL_KEY.VK_NUMPAD4,
        Key.NumPad5 => VIRTUAL_KEY.VK_NUMPAD5,
        Key.NumPad6 => VIRTUAL_KEY.VK_NUMPAD6,
        Key.NumPad7 => VIRTUAL_KEY.VK_NUMPAD7,
        Key.NumPad8 => VIRTUAL_KEY.VK_NUMPAD8,
        Key.NumPad9 => VIRTUAL_KEY.VK_NUMPAD9,
        Key.NumPadMultiply => VIRTUAL_KEY.VK_MULTIPLY,
        Key.NumPadAdd => VIRTUAL_KEY.VK_ADD,
        Key.NumPadSubtract => VIRTUAL_KEY.VK_SUBTRACT,
        Key.NumPadDecimal => VIRTUAL_KEY.VK_DECIMAL,
        Key.NumPadDivide => VIRTUAL_KEY.VK_DIVIDE,
        Key.NumLock => VIRTUAL_KEY.VK_NUMLOCK,

        // Symbols
        Key.OemSemicolon => VIRTUAL_KEY.VK_OEM_1,
        Key.OemPlus => VIRTUAL_KEY.VK_OEM_PLUS,
        Key.OemComma => VIRTUAL_KEY.VK_OEM_COMMA,
        Key.OemMinus => VIRTUAL_KEY.VK_OEM_MINUS,
        Key.OemPeriod => VIRTUAL_KEY.VK_OEM_PERIOD,
        Key.OemQuestion => VIRTUAL_KEY.VK_OEM_2,
        Key.OemTilde => VIRTUAL_KEY.VK_OEM_3,
        Key.OemOpenBrackets => VIRTUAL_KEY.VK_OEM_4,
        Key.OemPipe => VIRTUAL_KEY.VK_OEM_5,
        Key.OemCloseBrackets => VIRTUAL_KEY.VK_OEM_6,
        Key.OemQuotes => VIRTUAL_KEY.VK_OEM_7,

        // Media keys
        Key.VolumeMute => VIRTUAL_KEY.VK_VOLUME_MUTE,
        Key.VolumeDown => VIRTUAL_KEY.VK_VOLUME_DOWN,
        Key.VolumeUp => VIRTUAL_KEY.VK_VOLUME_UP,
        Key.MediaNext => VIRTUAL_KEY.VK_MEDIA_NEXT_TRACK,
        Key.MediaPrev => VIRTUAL_KEY.VK_MEDIA_PREV_TRACK,
        Key.MediaStop => VIRTUAL_KEY.VK_MEDIA_STOP,
        Key.MediaPlayPause => VIRTUAL_KEY.VK_MEDIA_PLAY_PAUSE,

        // Browser keys
        Key.BrowserBack => VIRTUAL_KEY.VK_BROWSER_BACK,
        Key.BrowserForward => VIRTUAL_KEY.VK_BROWSER_FORWARD,
        Key.BrowserRefresh => VIRTUAL_KEY.VK_BROWSER_REFRESH,
        Key.BrowserStop => VIRTUAL_KEY.VK_BROWSER_STOP,
        Key.BrowserSearch => VIRTUAL_KEY.VK_BROWSER_SEARCH,
        Key.BrowserFavorites => VIRTUAL_KEY.VK_BROWSER_FAVORITES,
        Key.BrowserHome => VIRTUAL_KEY.VK_BROWSER_HOME,

        // System keys
        Key.Sleep => VIRTUAL_KEY.VK_SLEEP,
        Key.LaunchMail => VIRTUAL_KEY.VK_LAUNCH_MAIL,
        Key.LaunchApp1 => VIRTUAL_KEY.VK_LAUNCH_APP1,
        Key.LaunchApp2 => VIRTUAL_KEY.VK_LAUNCH_APP2,

        _ => throw new HarnessException($"Unsupported key: {key}"),
    };
}
