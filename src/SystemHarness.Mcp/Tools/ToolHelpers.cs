namespace SystemHarness.Mcp.Tools;

public static class ToolHelpers
{
    /// <summary>
    /// Find a window by handle (numeric string) or by title substring match.
    /// </summary>
    public static WindowInfo? FindWindow(IReadOnlyList<WindowInfo> windows, string titleOrHandle)
    {
        if (nint.TryParse(titleOrHandle, out var handle))
        {
            var byHandle = windows.FirstOrDefault(w => w.Handle == handle);
            if (byHandle is not null) return byHandle;
        }

        return windows.FirstOrDefault(w =>
            w.Title.Contains(titleOrHandle, StringComparison.OrdinalIgnoreCase));
    }
}
