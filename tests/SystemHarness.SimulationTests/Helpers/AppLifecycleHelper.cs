namespace SystemHarness.SimulationTests.Helpers;

/// <summary>
/// Helper for managing application lifecycle in simulation tests.
/// Provides app start → ready wait → interaction → graceful shutdown pattern.
/// </summary>
public static class AppLifecycleHelper
{
    /// <summary>
    /// Starts an application, waits for its window to appear, and returns the process info.
    /// </summary>
    public static async Task<ProcessInfo> StartAndWaitAsync(
        IProcessManager process, IWindow window,
        string path, string? arguments = null,
        string? expectedWindowTitle = null,
        TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);

        var info = await process.StartAsync(path, arguments);

        if (expectedWindowTitle is not null)
        {
            await window.WaitForWindowAsync(expectedWindowTitle, timeout);
        }
        else
        {
            // Wait for any window from this process
            var deadline = DateTime.UtcNow + timeout.Value;
            while (DateTime.UtcNow < deadline)
            {
                var windows = await window.FindByProcessIdAsync(info.Pid);
                if (windows.Count > 0) return info;
                await Task.Delay(250);
            }
        }

        return info;
    }

    /// <summary>
    /// Gracefully closes an application by sending WM_CLOSE, then force-kills if it doesn't exit.
    /// </summary>
    public static async Task GracefulShutdownAsync(
        IProcessManager process, IWindow window,
        string titleOrHandle, int pid,
        TimeSpan? gracePeriod = null)
    {
        gracePeriod ??= TimeSpan.FromSeconds(3);

        try
        {
            await window.CloseAsync(titleOrHandle);
            var exited = await process.WaitForExitAsync(pid, gracePeriod);

            if (!exited)
            {
                await process.KillAsync(pid);
            }
        }
        catch
        {
            // Force kill on any error
            try { await process.KillAsync(pid); } catch { }
        }
    }
}
