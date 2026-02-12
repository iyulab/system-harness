using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using SystemHarness.Windows;

namespace SystemHarness.Tests;

/// <summary>
/// Helper for gracefully closing Notepad instances started by test fixtures.
/// Uses UI Automation to reliably dismiss the "Don't Save" dialog
/// across Windows 10/11 and all locales.
/// Falls back to process kill if all graceful methods fail.
/// </summary>
internal static class NotepadHelper
{
    /// <summary>
    /// Known button names for "Don't Save" across locales.
    /// </summary>
    private static readonly string[] DontSaveNames =
    [
        "Don't Save",   // English
        "Don\u2019t Save", // English with curly apostrophe (Win11)
        "저장 안 함",    // Korean
        "保存しない",    // Japanese
        "不保存",        // Chinese Simplified
        "Nicht speichern", // German
        "Ne pas enregistrer", // French
        "No guardar",   // Spanish
    ];

    /// <summary>
    /// Known AutomationIds for "Don't Save" button in Win11 Store Notepad.
    /// </summary>
    private static readonly string[] DontSaveAutomationIds =
    [
        "SecondaryDontSaveButton",
        "DontSaveButton",
        "CommandSecondary",
    ];

    /// <summary>
    /// Gracefully closes Notepad windows belonging to the given PID.
    /// Sends WM_CLOSE, then uses UI Automation to find and click "Don't Save"
    /// if a save dialog appears. Falls back to process kill as last resort.
    /// </summary>
    public static async Task CloseNotepadByPidAsync(int pid)
    {
        var window = new WindowsWindow();

        // Step 1: Find windows belonging to this PID
        IReadOnlyList<WindowInfo> wins;
        try { wins = await window.FindByProcessIdAsync(pid); }
        catch { return; }

        if (wins.Count == 0)
            return;

        // Step 2: Send WM_CLOSE to each window
        foreach (var w in wins)
        {
            try { await window.CloseAsync(w.Handle.ToString()); }
            catch { /* already gone */ }
        }

        await Task.Delay(800);

        // Step 3: Check if process still has windows (save dialog may be showing)
        try { wins = await window.FindByProcessIdAsync(pid); }
        catch { return; }

        if (wins.Count == 0)
            return;

        // Step 4: Use UI Automation to find and click "Don't Save"
        await DismissSaveDialogViaUIA(wins);

        // Step 5: Verify closed, retry once if needed
        await Task.Delay(500);
        try { wins = await window.FindByProcessIdAsync(pid); }
        catch { return; }

        if (wins.Count > 0)
        {
            await DismissSaveDialogViaKeyboard(window, wins);
        }

        // Step 6: Final check — force kill if still alive
        await Task.Delay(500);
        ForceKillProcess(pid);
    }

    /// <summary>
    /// Captures a snapshot of all current Notepad window handles.
    /// Call before starting a new Notepad instance. Then use
    /// <see cref="CloseNewNotepadWindowsAsync"/> to close only windows that appeared after this snapshot.
    /// This handles the Win11 Store Notepad PID mismatch problem safely.
    /// </summary>
    public static async Task<HashSet<nint>> SnapshotNotepadHandlesAsync()
    {
        var window = new WindowsWindow();
        var allWindows = await window.ListAsync();
        return allWindows
            .Where(w => IsNotepadTitle(w.Title))
            .Select(w => w.Handle)
            .ToHashSet();
    }

    /// <summary>
    /// Closes Notepad windows that were NOT present in the given snapshot.
    /// Safe: only closes windows created after the snapshot was taken.
    /// Handles "Don't Save" dialog via UI Automation.
    /// Falls back to process kill as last resort.
    /// </summary>
    public static async Task CloseNewNotepadWindowsAsync(HashSet<nint> beforeHandles)
    {
        var window = new WindowsWindow();

        // Win11 Store Notepad: the window may take 1-2s to appear after the launcher starts.
        List<WindowInfo> newNotepadWindows = [];
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var allWindows = await window.ListAsync();
            newNotepadWindows = allWindows
                .Where(w => IsNotepadTitle(w.Title) && !beforeHandles.Contains(w.Handle))
                .ToList();

            if (newNotepadWindows.Count > 0)
                break;

            await Task.Delay(500);
        }

        if (newNotepadWindows.Count == 0)
            return;

        // Collect PIDs for force kill fallback
        var pidsToKill = newNotepadWindows
            .Select(w => w.ProcessId)
            .Where(pid => pid > 0)
            .Distinct()
            .ToList();

        // Send WM_CLOSE to each new Notepad window
        foreach (var w in newNotepadWindows)
        {
            try { await window.CloseAsync(w.Handle.ToString()); }
            catch { /* already gone */ }
        }

        await Task.Delay(800);

        // Re-check for survivors (save dialog may have appeared)
        var remaining = await window.ListAsync();
        var survivors = remaining
            .Where(w => IsNotepadTitle(w.Title) && !beforeHandles.Contains(w.Handle))
            .ToList();

        if (survivors.Count == 0)
            return;

        // Use UIA to dismiss "Don't Save" dialogs
        await DismissSaveDialogViaUIA(survivors);

        await Task.Delay(500);

        // Keyboard fallback
        remaining = await window.ListAsync();
        survivors = remaining
            .Where(w => IsNotepadTitle(w.Title) && !beforeHandles.Contains(w.Handle))
            .ToList();

        if (survivors.Count > 0)
        {
            await DismissSaveDialogViaKeyboard(window, survivors);
        }

        // Final fallback: force kill any remaining processes
        await Task.Delay(500);
        remaining = await window.ListAsync();
        survivors = remaining
            .Where(w => IsNotepadTitle(w.Title) && !beforeHandles.Contains(w.Handle))
            .ToList();

        if (survivors.Count > 0)
        {
            foreach (var pid in pidsToKill)
            {
                ForceKillProcess(pid);
            }
            // Also kill by window PID (in case of PID changes in Store Notepad)
            foreach (var s in survivors)
            {
                if (s.ProcessId > 0)
                    ForceKillProcess(s.ProcessId);
            }
        }
    }

    /// <summary>
    /// Uses FlaUI to find buttons matching "Don't Save" in any locale and invokes them.
    /// Searches by Name AND by AutomationId (for Win11 Store Notepad ContentDialog).
    /// </summary>
    private static async Task DismissSaveDialogViaUIA(IReadOnlyList<WindowInfo> wins)
    {
        await Task.Run(() =>
        {
            using var automation = new UIA3Automation();

            foreach (var w in wins)
            {
                try
                {
                    var windowElement = automation.FromHandle(w.Handle);
                    if (windowElement is null) continue;

                    // Find all Button descendants
                    var buttons = windowElement.FindAll(
                        TreeScope.Descendants,
                        automation.ConditionFactory.ByControlType(ControlType.Button));

                    AutomationElement? dontSaveButton = null;

                    foreach (var btn in buttons)
                    {
                        var name = btn.Properties.Name.ValueOrDefault ?? "";
                        var automationId = btn.Properties.AutomationId.ValueOrDefault ?? "";

                        // Match by button name (locale-aware)
                        if (IsDontSaveButton(name))
                        {
                            dontSaveButton = btn;
                            break;
                        }

                        // Match by AutomationId (Win11 Store Notepad)
                        if (IsDontSaveAutomationId(automationId))
                        {
                            dontSaveButton = btn;
                            break;
                        }
                    }

                    if (dontSaveButton is not null)
                    {
                        if (dontSaveButton.Patterns.Invoke.IsSupported)
                        {
                            dontSaveButton.Patterns.Invoke.Pattern.Invoke();
                        }
                        else
                        {
                            dontSaveButton.Click();
                        }
                    }
                }
                catch
                {
                    // Window may have closed during iteration
                }
            }
        });
    }

    /// <summary>
    /// Keyboard fallback: focus the window and try common Don't Save shortcuts.
    /// Tries multiple strategies: Alt+N (Win10), Alt+D (some builds), Tab+Enter.
    /// </summary>
    private static async Task DismissSaveDialogViaKeyboard(WindowsWindow window, IReadOnlyList<WindowInfo> wins)
    {
        var keyboard = new WindowsKeyboard();

        foreach (var w in wins)
        {
            try
            {
                await window.FocusAsync(w.Handle.ToString());
                await Task.Delay(300);

                // Strategy 1: Alt+N (classic Windows "Don't Save" hotkey)
                await keyboard.HotkeyAsync(default, Key.Alt, Key.N);
                await Task.Delay(500);

                if (!await IsWindowAlive(window, w.Handle))
                    continue;

                // Strategy 2: Alt+D (some locales/versions)
                await keyboard.HotkeyAsync(default, Key.Alt, Key.D);
                await Task.Delay(500);

                if (!await IsWindowAlive(window, w.Handle))
                    continue;

                // Strategy 3: Tab to "Don't Save" button + Enter
                await keyboard.KeyPressAsync(Key.Tab);
                await Task.Delay(100);
                await keyboard.KeyPressAsync(Key.Enter);
                await Task.Delay(500);

                if (!await IsWindowAlive(window, w.Handle))
                    continue;

                // Strategy 4: Multiple Tab presses (dialog might have 3 buttons)
                await keyboard.KeyPressAsync(Key.Tab);
                await Task.Delay(100);
                await keyboard.KeyPressAsync(Key.Tab);
                await Task.Delay(100);
                await keyboard.KeyPressAsync(Key.Enter);
                await Task.Delay(500);
            }
            catch { /* already gone */ }
        }
    }

    private static async Task<bool> IsWindowAlive(WindowsWindow window, nint handle)
    {
        try
        {
            var allWindows = await window.ListAsync();
            return allWindows.Any(a => a.Handle == handle);
        }
        catch { return false; }
    }

    private static bool IsNotepadTitle(string title) =>
        title.Contains("Notepad", StringComparison.OrdinalIgnoreCase) ||
        title.Contains("메모장", StringComparison.OrdinalIgnoreCase) ||
        title == "제목 없음 - 메모장";

    private static bool IsDontSaveButton(string name)
    {
        foreach (var candidate in DontSaveNames)
        {
            if (name.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsDontSaveAutomationId(string automationId)
    {
        foreach (var candidate in DontSaveAutomationIds)
        {
            if (string.Equals(automationId, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Force kills a process by PID. Silent on failure.
    /// </summary>
    private static void ForceKillProcess(int pid)
    {
        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById(pid);
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
            }
        }
        catch { /* process may already be gone */ }
    }
}
