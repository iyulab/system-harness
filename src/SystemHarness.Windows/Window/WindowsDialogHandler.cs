using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IDialogHandler"/> using Win32 APIs.
/// Handles common system dialogs (message boxes, file dialogs).
/// </summary>
public sealed class WindowsDialogHandler : IDialogHandler
{
    private readonly IKeyboard _keyboard;

    public WindowsDialogHandler() => _keyboard = new WindowsKeyboard();

    internal WindowsDialogHandler(IKeyboard keyboard) => _keyboard = keyboard;

    public Task<bool> IsDialogOpenAsync(string? parentTitleOrHandle = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            // Search for dialog windows (class #32770 = standard dialog)
            var found = false;

            unsafe
            {
                PInvoke.EnumWindows((hwnd, _) =>
                {
                    if (!PInvoke.IsWindowVisible(hwnd))
                        return true;

                    var classBuffer = new char[256];
                    fixed (char* pClass = classBuffer)
                    {
                        var classLen = PInvoke.GetClassName(hwnd, pClass, 256);
                        if (classLen > 0)
                        {
                            var className = new string(classBuffer, 0, classLen);
                            if (className == "#32770") // Dialog class
                            {
                                if (parentTitleOrHandle is null)
                                {
                                    found = true;
                                    return false; // stop enumeration
                                }

                                // Check if this dialog belongs to the parent
                                var parent = PInvoke.GetParent(hwnd);
                                if (!parent.IsNull)
                                {
                                    var titleLength = PInvoke.GetWindowTextLength(parent);
                                    if (titleLength > 0)
                                    {
                                        var titleBuffer = new char[titleLength + 1];
                                        int copied;
                                        fixed (char* pTitle = titleBuffer)
                                        {
                                            copied = PInvoke.GetWindowText(parent, pTitle, titleLength + 1);
                                        }
                                        if (copied > 0)
                                        {
                                            var title = new string(titleBuffer, 0, copied);
                                            if (title.Contains(parentTitleOrHandle, StringComparison.OrdinalIgnoreCase))
                                            {
                                                found = true;
                                                return false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    return true;
                }, 0);
            }

            return found;
        }, ct);
    }

    public async Task SetFileDialogPathAsync(string path, CancellationToken ct = default)
    {
        // In a standard file dialog, the file name edit control is usually focused
        // Type the path and press Enter
        await _keyboard.TypeAsync(path, ct: ct);
        await Task.Delay(200, ct);
        await _keyboard.KeyPressAsync(Key.Enter, ct);
    }

    public async Task ClickDialogButtonAsync(string buttonText, CancellationToken ct = default)
    {
        // Use Alt+key shortcut if available, or Tab+Enter to navigate
        // For common buttons: OK (Enter), Cancel (Escape), Yes (Alt+Y), No (Alt+N)
        switch (buttonText.ToUpperInvariant())
        {
            case "OK":
                await _keyboard.KeyPressAsync(Key.Enter, ct);
                break;
            case "CANCEL":
                await _keyboard.KeyPressAsync(Key.Escape, ct);
                break;
            case "YES":
                await _keyboard.HotkeyAsync(ct, Key.Alt, Key.Y);
                break;
            case "NO":
                await _keyboard.HotkeyAsync(ct, Key.Alt, Key.N);
                break;
            case "SAVE":
                await _keyboard.HotkeyAsync(ct, Key.Alt, Key.S);
                break;
            case "DON'T SAVE":
            case "DONT SAVE":
                await _keyboard.HotkeyAsync(ct, Key.Alt, Key.N);
                break;
            default:
                // Try Tab navigation to find the button
                for (var i = 0; i < 10; i++)
                {
                    await _keyboard.KeyPressAsync(Key.Tab, ct);
                    await Task.Delay(100, ct);
                }
                await _keyboard.KeyPressAsync(Key.Enter, ct);
                break;
        }
    }

    public async Task DismissMessageBoxAsync(string? buttonText = null, CancellationToken ct = default)
    {
        if (buttonText is not null)
        {
            await ClickDialogButtonAsync(buttonText, ct);
        }
        else
        {
            // Default: press Escape to dismiss
            await _keyboard.KeyPressAsync(Key.Escape, ct);
        }
    }
}
