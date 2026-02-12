namespace SystemHarness;

/// <summary>
/// Keyboard input simulation — type text, press keys, hotkeys, and key state queries.
/// </summary>
public interface IKeyboard
{
    /// <summary>
    /// Types a string of text. For short text, uses per-character input.
    /// For long text or CJK, may use clipboard paste internally.
    /// </summary>
    Task TypeAsync(string text, int delayMs = 0, CancellationToken ct = default);

    /// <summary>
    /// Presses and releases a single key.
    /// </summary>
    Task KeyPressAsync(Key key, CancellationToken ct = default);

    /// <summary>
    /// Holds a key down (must be followed by KeyUpAsync).
    /// </summary>
    Task KeyDownAsync(Key key, CancellationToken ct = default);

    /// <summary>
    /// Releases a held key.
    /// </summary>
    Task KeyUpAsync(Key key, CancellationToken ct = default);

    /// <summary>
    /// Presses a key combination (e.g., Ctrl+S, Ctrl+Shift+N).
    /// All modifier keys are held, the last key is pressed, then all are released.
    /// </summary>
    Task HotkeyAsync(CancellationToken ct = default, params Key[] keys);

    // --- Phase 9 Extensions (DIM for backward compatibility) ---

    /// <summary>
    /// Checks whether a specific key is currently pressed down.
    /// Uses GetAsyncKeyState on Windows.
    /// </summary>
    Task<bool> IsKeyPressedAsync(Key key, CancellationToken ct = default)
        => throw new NotSupportedException("IsKeyPressedAsync is not supported by this implementation.");

    /// <summary>
    /// Toggles a lock key (CapsLock, NumLock, ScrollLock) to the specified state.
    /// </summary>
    Task ToggleKeyAsync(Key key, bool state, CancellationToken ct = default)
        => throw new NotSupportedException("ToggleKeyAsync is not supported by this implementation.");
}
