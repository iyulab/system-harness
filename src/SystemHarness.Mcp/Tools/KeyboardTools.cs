using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class KeyboardTools(IHarness harness)
{
    [McpServerTool(Name = "keyboard_type"), Description("Type text as keyboard input. Optional delay between characters in ms.")]
    public async Task<string> TypeAsync(
        [Description("The text string to type.")] string text,
        [Description("Delay between each character in milliseconds. 0 for instant.")] int delayMs = 0,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrEmpty(text))
            return McpResponse.Error("invalid_parameter", "text cannot be empty.", sw.ElapsedMilliseconds);
        if (delayMs < 0)
            return McpResponse.Error("invalid_parameter", $"delayMs cannot be negative (got {delayMs}).", sw.ElapsedMilliseconds);
        await harness.Keyboard.TypeAsync(text, delayMs, ct);
        ActionLog.Record("keyboard_type", $"len={text.Length}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Typed {text.Length} characters.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "keyboard_press"), Description("Press a single key (e.g., 'Enter', 'Tab', 'Escape', 'F5').")]
    public async Task<string> PressAsync(
        [Description("Key name (PascalCase). Common: Enter, Tab, Escape, Space, Backspace, Delete, Home, End, PageUp, PageDown, Up, Down, Left, Right, F1-F12, A-Z, D0-D9, Ctrl, Alt, Shift, Win.")] string key,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var k))
            return McpResponse.Error("invalid_key", $"Unknown key: '{key}'. Valid keys: {string.Join(", ", Enum.GetNames<Key>())}");
        var sw = Stopwatch.StartNew();
        await harness.Keyboard.KeyPressAsync(k, ct);
        ActionLog.Record("keyboard_press", $"key={key}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Pressed {key}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "keyboard_key_down"), Description("Hold a key down (must be followed by keyboard_key_up). Useful for modifier holds during drag operations.")]
    public async Task<string> KeyDownAsync(
        [Description("Key name (PascalCase). Common: Ctrl, Alt, Shift, Win, A-Z.")] string key,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var k))
            return McpResponse.Error("invalid_key", $"Unknown key: '{key}'. Valid keys: {string.Join(", ", Enum.GetNames<Key>())}");
        var sw = Stopwatch.StartNew();
        await harness.Keyboard.KeyDownAsync(k, ct);
        ActionLog.Record("keyboard_key_down", $"key={key}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Key down: {key}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "keyboard_key_up"), Description("Release a held key (pair with keyboard_key_down).")]
    public async Task<string> KeyUpAsync(
        [Description("Key name (PascalCase) to release.")] string key,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var k))
            return McpResponse.Error("invalid_key", $"Unknown key: '{key}'. Valid keys: {string.Join(", ", Enum.GetNames<Key>())}");
        var sw = Stopwatch.StartNew();
        await harness.Keyboard.KeyUpAsync(k, ct);
        ActionLog.Record("keyboard_key_up", $"key={key}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Key up: {key}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "keyboard_is_pressed"), Description("Check if a specific key is currently pressed down.")]
    public async Task<string> IsKeyPressedAsync(
        [Description("Key name (PascalCase) to check.")] string key,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var k))
            return McpResponse.Error("invalid_key", $"Unknown key: '{key}'. Valid keys: {string.Join(", ", Enum.GetNames<Key>())}");
        var sw = Stopwatch.StartNew();
        var pressed = await harness.Keyboard.IsKeyPressedAsync(k, ct);
        return McpResponse.Check(pressed, pressed ? $"'{key}' is pressed." : $"'{key}' is not pressed.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "keyboard_toggle_lock"), Description("Toggle a lock key (CapsLock, NumLock, ScrollLock) to the specified state.")]
    public async Task<string> ToggleLockAsync(
        [Description("Lock key name: 'CapsLock', 'NumLock', or 'ScrollLock'.")] string key,
        [Description("Desired state: true for on, false for off.")] bool state,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<Key>(key, ignoreCase: true, out var k))
            return McpResponse.Error("invalid_key", $"Unknown key: '{key}'. Use CapsLock, NumLock, or ScrollLock.");
        var sw = Stopwatch.StartNew();
        await harness.Keyboard.ToggleKeyAsync(k, state, ct);
        ActionLog.Record("keyboard_toggle_lock", $"key={key}, state={state}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Toggled {key} to {(state ? "ON" : "OFF")}.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "keyboard_hotkey"), Description("Press a key combination (e.g., 'Ctrl+C', 'Alt+F4', 'Ctrl+Shift+S'). Keys separated by '+'.")]
    public async Task<string> HotkeyAsync(
        [Description("Key combination with '+' separator. Use PascalCase key names (e.g., 'Ctrl+A', 'Ctrl+Shift+S', 'Alt+F4').")] string keys,
        CancellationToken ct = default)
    {
        var parts = keys.Split('+', StringSplitOptions.TrimEntries);
        var keyArray = new Key[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!Enum.TryParse<Key>(parts[i], ignoreCase: true, out keyArray[i]))
                return McpResponse.Error("invalid_key", $"Unknown key: '{parts[i]}'. Valid keys: {string.Join(", ", Enum.GetNames<Key>())}");
        }
        var sw = Stopwatch.StartNew();
        await harness.Keyboard.HotkeyAsync(ct, keyArray);
        ActionLog.Record("keyboard_hotkey", $"keys={keys}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Pressed hotkey: {keys}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "keyboard_hotkey_wait"), Description(
        "Press a hotkey then wait for an expected change. " +
        "expectType: 'window' (new window with title containing expectValue), " +
        "'title_change' (target window title changes to contain expectValue), " +
        "'text' (OCR detects expectValue on screen). " +
        "Returns when the expected change is detected or timeout is reached.")]
    public async Task<string> HotkeyWaitAsync(
        [Description("Key combination with '+' separator (e.g., 'Ctrl+S', 'Alt+F4').")] string keys,
        [Description("What to wait for: 'window' (new window), 'title_change' (title update), or 'text' (OCR text).")] string expectType,
        [Description("Value to match: window title substring, new title substring, or OCR text to find.")] string expectValue,
        [Description("Target window for 'title_change' (title substring or handle). Required for title_change.")] string? titleOrHandle = null,
        [Description("Maximum time to wait in milliseconds.")] int timeoutMs = 10000,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(keys))
            return McpResponse.Error("invalid_parameter", "keys cannot be empty.", sw.ElapsedMilliseconds);
        if (string.IsNullOrWhiteSpace(expectValue))
            return McpResponse.Error("invalid_parameter", "expectValue cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);

        // Parse and press hotkey
        var parts = keys.Split('+', StringSplitOptions.TrimEntries);
        var keyArray = new Key[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!Enum.TryParse<Key>(parts[i], ignoreCase: true, out keyArray[i]))
                return McpResponse.Error("invalid_key", $"Unknown key: '{parts[i]}'.");
        }

        await harness.Keyboard.HotkeyAsync(ct, keyArray);
        ActionLog.Record("keyboard_hotkey_wait", $"keys={keys}, expect={expectType}:{expectValue}", sw.ElapsedMilliseconds, true);

        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(300);
        var attempts = 0;

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(interval, ct);
            attempts++;

            switch (expectType.ToLowerInvariant())
            {
                case "window":
                {
                    var windows = await harness.Window.ListAsync(ct);
                    var win = windows.FirstOrDefault(w =>
                        w.Title.Contains(expectValue, StringComparison.OrdinalIgnoreCase));
                    if (win is not null)
                        return McpResponse.Ok(new
                        {
                            hotkey = keys, detected = true, expectType, expectValue,
                            window = new { handle = win.Handle.ToString(), win.Title },
                            attempts,
                        }, sw.ElapsedMilliseconds);
                    break;
                }
                case "title_change":
                {
                    if (titleOrHandle is null)
                        return McpResponse.Error("missing_window",
                            "titleOrHandle is required for 'title_change' expectType.", sw.ElapsedMilliseconds);
                    var windows = await harness.Window.ListAsync(ct);
                    var target = ToolHelpers.FindWindow(windows, titleOrHandle);
                    if (target is not null && target.Title.Contains(expectValue, StringComparison.OrdinalIgnoreCase))
                        return McpResponse.Ok(new
                        {
                            hotkey = keys, detected = true, expectType, expectValue,
                            newTitle = target.Title, attempts,
                        }, sw.ElapsedMilliseconds);
                    break;
                }
                case "text":
                {
                    var result = await harness.Ocr.RecognizeScreenAsync(ct: ct);
                    var match = result.Lines.FirstOrDefault(l =>
                        l.Text.Contains(expectValue, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                        return McpResponse.Ok(new
                        {
                            hotkey = keys, detected = true, expectType, expectValue,
                            matchedText = match.Text,
                            bounds = new { match.BoundingRect.X, match.BoundingRect.Y, match.BoundingRect.Width, match.BoundingRect.Height },
                            attempts,
                        }, sw.ElapsedMilliseconds);
                    break;
                }
                default:
                    return McpResponse.Error("invalid_expect_type",
                        $"Unknown expectType '{expectType}'. Use 'window', 'title_change', or 'text'.",
                        sw.ElapsedMilliseconds);
            }
        }

        return McpResponse.Ok(new
        {
            hotkey = keys, detected = false, expectType, expectValue, attempts,
        }, sw.ElapsedMilliseconds);
    }
}
