using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class AppTools(IHarness harness)
{
    [McpServerTool(Name = "app_open"), Description(
        "Open a file or application and wait for its window to appear. " +
        "Combines process start with window wait. Returns the window handle and info.")]
    public async Task<string> OpenAsync(
        [Description("Executable path or file path to open.")] string path,
        [Description("Optional command-line arguments.")] string? arguments = null,
        [Description("Max time to wait for window to appear in milliseconds.")] int timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(path))
            return McpResponse.Error("invalid_parameter", "path cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);
        var proc = await harness.Process.StartAsync(path, arguments, ct);

        // Wait for a window from this process
        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(500);

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(interval, ct);

            var windows = await harness.Window.ListAsync(ct);
            var win = windows.FirstOrDefault(w => w.ProcessId == proc.Pid && !string.IsNullOrEmpty(w.Title));
            if (win is not null)
            {
                ActionLog.Record("app_open", $"path={path}", sw.ElapsedMilliseconds, true);
                return McpResponse.Ok(new
                {
                    opened = true,
                    process = new { proc.Pid, proc.Name },
                    window = new
                    {
                        handle = win.Handle.ToString(),
                        win.Title,
                        bounds = new { win.Bounds.X, win.Bounds.Y, win.Bounds.Width, win.Bounds.Height },
                    },
                }, sw.ElapsedMilliseconds);
            }
        }

        ActionLog.Record("app_open", $"path={path}", sw.ElapsedMilliseconds, false);
        return McpResponse.Ok(new
        {
            opened = false,
            process = new { proc.Pid, proc.Name },
            window = (object?)null,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "app_close"), Description(
        "Close an application window and wait for it to disappear. " +
        "Optionally handles save dialogs by dismissing them.")]
    public async Task<string> CloseAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        [Description("If true, automatically clicks 'Don't Save'/'No' on save dialogs.")] bool dismissSaveDialog = false,
        [Description("Max time to wait for window to disappear in milliseconds.")] int timeoutMs = 10000,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);

        await harness.Window.CloseAsync(titleOrHandle, ct);

        // Wait for window to disappear
        var deadline = TimeSpan.FromMilliseconds(timeoutMs);
        var interval = TimeSpan.FromMilliseconds(500);

        while (sw.Elapsed < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(interval, ct);

            var windows = await harness.Window.ListAsync(ct);
            var win = ToolHelpers.FindWindow(windows, titleOrHandle);

            if (win is null)
            {
                ActionLog.Record("app_close", $"window={titleOrHandle}", sw.ElapsedMilliseconds, true);
                return McpResponse.Ok(new
                {
                    closed = true,
                    titleOrHandle,
                }, sw.ElapsedMilliseconds);
            }

            // If a save dialog appeared and we should dismiss it
            if (dismissSaveDialog)
            {
                try
                {
                    var isDialog = await harness.DialogHandler.IsDialogOpenAsync(titleOrHandle, ct);
                    if (isDialog)
                    {
                        // Try "Don't Save" first, then "No"
                        try { await harness.DialogHandler.ClickDialogButtonAsync("Don't Save", ct); }
                        catch { await harness.DialogHandler.ClickDialogButtonAsync("No", ct); }
                    }
                }
                catch { /* Dialog handling is best-effort */ }
            }
        }

        ActionLog.Record("app_close", $"window={titleOrHandle}", sw.ElapsedMilliseconds, false);
        return McpResponse.Ok(new
        {
            closed = false,
            titleOrHandle,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "app_focus"), Description(
        "Focus a window and verify it became the foreground window. " +
        "Returns the window info after focusing.")]
    public async Task<string> FocusAsync(
        [Description("Window title (substring match, case-insensitive) or handle.")] string titleOrHandle,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(titleOrHandle))
            return McpResponse.Error("invalid_parameter", "titleOrHandle cannot be empty.", sw.ElapsedMilliseconds);

        await harness.Window.FocusAsync(titleOrHandle, ct);
        await Task.Delay(100, ct); // Brief settle

        var fg = await harness.Window.GetForegroundAsync(ct);
        var windows = await harness.Window.ListAsync(ct);
        var target = ToolHelpers.FindWindow(windows, titleOrHandle);

        var verified = fg is not null && target is not null && fg.Handle == target.Handle;
        ActionLog.Record("app_focus", $"window={titleOrHandle}, verified={verified}", sw.ElapsedMilliseconds, verified);

        return McpResponse.Ok(new
        {
            focused = verified,
            window = target is not null ? new
            {
                handle = target.Handle.ToString(),
                target.Title,
                target.ProcessId,
                bounds = new { target.Bounds.X, target.Bounds.Y, target.Bounds.Width, target.Bounds.Height },
            } : null,
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "dialog_check"), Description("Check whether a dialog or message box is currently open, optionally for a specific parent window.")]
    public async Task<string> CheckDialogAsync(
        [Description("Parent window title or handle to check for dialogs. Omit to check any.")] string? parentTitleOrHandle = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var isOpen = await harness.DialogHandler.IsDialogOpenAsync(parentTitleOrHandle, ct);
        return McpResponse.Check(isOpen, isOpen ? "A dialog is open." : "No dialog detected.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "dialog_click"), Description("Click a specific button in a dialog by its text label (e.g., 'OK', 'Cancel', 'Save', 'Yes', 'No').")]
    public async Task<string> ClickDialogAsync(
        [Description("Button text to click.")] string buttonText,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(buttonText))
            return McpResponse.Error("invalid_parameter", "buttonText cannot be empty.", sw.ElapsedMilliseconds);
        await harness.DialogHandler.ClickDialogButtonAsync(buttonText, ct);
        ActionLog.Record("dialog_click", $"button={buttonText}", sw.ElapsedMilliseconds, true);
        return McpResponse.Confirm($"Clicked dialog button: {buttonText}", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "dialog_dismiss"), Description(
        "Dismiss a dialog or message box by clicking a button with the given text. " +
        "If no buttonText is provided, tries 'OK' then 'Close'. " +
        "Optionally specify the parent window.")]
    public async Task<string> DismissDialogAsync(
        [Description("Button text to click (e.g., 'OK', 'Cancel', 'Yes', 'No'). Tries 'OK' then 'Close' if omitted.")] string? buttonText = null,
        [Description("Parent window title or handle to check for dialogs.")] string? parentTitleOrHandle = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var isOpen = await harness.DialogHandler.IsDialogOpenAsync(parentTitleOrHandle, ct);
        if (!isOpen)
            return McpResponse.Ok(new { dismissed = false, reason = "No dialog detected." }, sw.ElapsedMilliseconds);

        if (buttonText is not null)
        {
            await harness.DialogHandler.ClickDialogButtonAsync(buttonText, ct);
        }
        else
        {
            // Try common dismiss buttons
            try { await harness.DialogHandler.DismissMessageBoxAsync(null, ct); }
            catch
            {
                try { await harness.DialogHandler.ClickDialogButtonAsync("OK", ct); }
                catch { await harness.DialogHandler.ClickDialogButtonAsync("Close", ct); }
            }
        }

        ActionLog.Record("dialog_dismiss", $"button={buttonText ?? "(auto)"}", sw.ElapsedMilliseconds, true);
        return McpResponse.Ok(new
        {
            dismissed = true,
            buttonText = buttonText ?? "(auto)",
        }, sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "dialog_fill_file"), Description(
        "Fill a file path into an open/save file dialog and confirm it. " +
        "Finds the filename input field, types the path, then clicks Save/Open. " +
        "Optionally specify the parent window.")]
    public async Task<string> FillFileDialogAsync(
        [Description("Full path of the file to enter in the dialog.")] string filePath,
        [Description("Parent window title or handle. Empty to auto-detect.")] string? parentTitleOrHandle = null,
        [Description("Confirm button text (e.g., 'Save', 'Open'). Auto-detected if omitted.")] string? confirmButton = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(filePath))
            return McpResponse.Error("invalid_parameter", "filePath cannot be empty.", sw.ElapsedMilliseconds);

        // Find the file dialog — look for the filename edit field
        var windowTitle = parentTitleOrHandle ?? "";
        // Common automation IDs for filename field in Windows file dialogs
        string[] fileNameIds = ["1001", "FileNameControlHost", "1148"];
        UIElement? fileNameField = null;

        foreach (var id in fileNameIds)
        {
            var condition = new UIElementCondition { AutomationId = id };
            fileNameField = await harness.UIAutomation.FindFirstAsync(windowTitle, condition, ct);
            if (fileNameField is not null) break;
        }

        // Fallback: find Edit control with common name patterns
        if (fileNameField is null)
        {
            var condition = new UIElementCondition { ControlType = UIControlType.Edit };
            var edits = await harness.UIAutomation.FindAllAsync(windowTitle, condition, ct);
            fileNameField = edits.FirstOrDefault(e =>
                e.Name.Contains("File name", StringComparison.OrdinalIgnoreCase) ||
                e.Name.Contains("파일 이름", StringComparison.OrdinalIgnoreCase));
        }

        if (fileNameField is null)
            return McpResponse.Error("filename_field_not_found",
                "Could not find the filename input field in the file dialog.", sw.ElapsedMilliseconds);

        // Click the field, select all, type the path
        var cx = fileNameField.BoundingRectangle.CenterX;
        var cy = fileNameField.BoundingRectangle.CenterY;
        await harness.Mouse.ClickAsync(cx, cy, MouseButton.Left, ct);
        await Task.Delay(50, ct);
        await harness.Keyboard.HotkeyAsync(ct, Key.Ctrl, Key.A);
        await Task.Delay(30, ct);
        await harness.Keyboard.TypeAsync(filePath, ct: ct);
        await Task.Delay(100, ct);

        // Click confirm button (Save / Open / OK)
        string[] confirmButtons = confirmButton is not null
            ? [confirmButton]
            : ["Save", "Open", "저장", "열기", "OK", "확인"];

        foreach (var btnText in confirmButtons)
        {
            var condition = new UIElementCondition { Name = btnText, ControlType = UIControlType.Button };
            var btn = await harness.UIAutomation.FindFirstAsync(windowTitle, condition, ct);
            if (btn is not null)
            {
                await harness.UIAutomation.InvokeAsync(btn, ct);
                ActionLog.Record("dialog_fill_file", $"path={filePath}, confirm={btnText}", sw.ElapsedMilliseconds, true);
                return McpResponse.Ok(new
                {
                    filled = true,
                    filePath,
                    confirmedWith = btnText,
                }, sw.ElapsedMilliseconds);
            }
        }

        // If no button found, try pressing Enter as fallback
        await harness.Keyboard.KeyPressAsync(Key.Enter, ct);
        ActionLog.Record("dialog_fill_file", $"path={filePath}, confirm=Enter", sw.ElapsedMilliseconds, true);

        return McpResponse.Ok(new
        {
            filled = true,
            filePath,
            confirmedWith = "Enter key",
        }, sw.ElapsedMilliseconds);
    }
}
