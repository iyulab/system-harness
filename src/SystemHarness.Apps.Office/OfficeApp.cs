using SystemHarness;

namespace SystemHarness.Apps.Office;

/// <summary>
/// Automate a running Office application using IHarness process/window/UIAutomation.
/// </summary>
public sealed class OfficeApp : IOfficeApp
{
    private readonly IHarness _harness;

    public OfficeApp(IHarness harness)
    {
        _harness = harness;
    }

    public async Task<int> OpenDocumentAsync(string filePath, CancellationToken ct = default)
    {
        var info = await _harness.Process.StartAsync(filePath);
        // Wait for the window to appear
        var fileName = Path.GetFileName(filePath);
        await _harness.Window.WaitForWindowAsync(fileName, TimeSpan.FromSeconds(15), ct);
        return info.Pid;
    }

    public async Task SaveAsync(string titleOrHandle, CancellationToken ct = default)
    {
        await _harness.Window.FocusAsync(titleOrHandle, ct);
        await Task.Delay(200, ct);
        await _harness.Keyboard.HotkeyAsync(ct, Key.Ctrl, Key.S);
        await Task.Delay(500, ct);
    }

    public async Task CloseAsync(string titleOrHandle, bool save = true, CancellationToken ct = default)
    {
        if (save)
        {
            await SaveAsync(titleOrHandle, ct);
        }

        await _harness.Window.CloseAsync(titleOrHandle, ct);
    }

    public async Task<UIElement> GetDocumentTreeAsync(string titleOrHandle, CancellationToken ct = default)
    {
        return await _harness.UIAutomation.GetAccessibilityTreeAsync(titleOrHandle, maxDepth: 3, ct);
    }
}
