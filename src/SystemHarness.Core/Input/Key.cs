namespace SystemHarness;

/// <summary>
/// Platform-independent key identifiers for keyboard simulation.
/// </summary>
public enum Key
{
    // Modifiers
    Ctrl,
    Alt,
    Shift,
    Win,

    // Function keys
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

    // Navigation
    Escape,
    Tab,
    CapsLock,
    Enter,
    Backspace,
    Delete,
    Insert,
    Home,
    End,
    PageUp,
    PageDown,
    Up,
    Down,
    Left,
    Right,

    // Special
    Space,
    PrintScreen,
    ScrollLock,
    Pause,
    Menu,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Numbers
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Numpad
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4,
    NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    NumPadMultiply, NumPadAdd, NumPadSubtract,
    NumPadDecimal, NumPadDivide, NumLock,

    // Symbols
    OemSemicolon,
    OemPlus,
    OemComma,
    OemMinus,
    OemPeriod,
    OemQuestion,
    OemTilde,
    OemOpenBrackets,
    OemPipe,
    OemCloseBrackets,
    OemQuotes,

    // Media keys
    VolumeMute,
    VolumeDown,
    VolumeUp,
    MediaNext,
    MediaPrev,
    MediaStop,
    MediaPlayPause,

    // Browser keys
    BrowserBack,
    BrowserForward,
    BrowserRefresh,
    BrowserStop,
    BrowserSearch,
    BrowserFavorites,
    BrowserHome,

    // System keys
    Sleep,
    LaunchMail,
    LaunchApp1,
    LaunchApp2,
}
