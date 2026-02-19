namespace SystemHarness.SimulationTests;

/// <summary>
/// Base class for simulation tests providing common harness access and helper methods.
/// </summary>
[Collection("Simulation")]
public abstract class SimulationTestBase : IAsyncLifetime
{
    protected SimulationFixture Fixture { get; }
    protected IHarness Harness => Fixture.Harness;
    protected IShell Shell => Harness.Shell;
    protected IProcessManager Process => Harness.Process;
    protected IFileSystem FileSystem => Harness.FileSystem;
    protected IWindow Window => Harness.Window;
    protected IClipboard Clipboard => Harness.Clipboard;
    protected IScreen Screen => Harness.Screen;
    protected IMouse Mouse => Harness.Mouse;
    protected IKeyboard Keyboard => Harness.Keyboard;
    protected IDisplay Display => Harness.Display;

    private readonly List<int> _launchedPids = new();

    protected SimulationTestBase(SimulationFixture fixture)
    {
        Fixture = fixture;
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        // Kill all processes launched during the test
        foreach (var pid in _launchedPids)
        {
            try { await Process.KillAsync(pid); } catch { }
        }
        _launchedPids.Clear();
    }

    /// <summary>
    /// Launches an application, tracks its PID for cleanup, and waits for its window to appear.
    /// </summary>
    protected async Task<ProcessInfo> LaunchAppAsync(string path, string? arguments = null, TimeSpan? windowTimeout = null)
    {
        var info = await Process.StartAsync(path, arguments);
        _launchedPids.Add(info.Pid);

        // Wait for window to appear
        windowTimeout ??= TimeSpan.FromSeconds(10);
        var deadline = DateTime.UtcNow + windowTimeout.Value;

        while (DateTime.UtcNow < deadline)
        {
            var windows = await Window.FindByProcessIdAsync(info.Pid);
            if (windows.Count > 0)
                return info;

            await Task.Delay(250);
        }

        return info;
    }

    /// <summary>
    /// Retries an action until it succeeds or the timeout expires.
    /// </summary>
    protected static async Task RetryAsync(Func<Task> action, TimeSpan? timeout = null, int delayMs = 250)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow + timeout.Value;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(delayMs);
            }
        }

        throw new HarnessException($"Retry timed out after {timeout.Value.TotalSeconds}s",
            lastException ?? new TimeoutException());
    }

    /// <summary>
    /// Retries an action that returns a value until it succeeds.
    /// </summary>
    protected static async Task<T> RetryAsync<T>(Func<Task<T>> action, TimeSpan? timeout = null, int delayMs = 250)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var deadline = DateTime.UtcNow + timeout.Value;
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(delayMs);
            }
        }

        throw new HarnessException($"Retry timed out after {timeout.Value.TotalSeconds}s",
            lastException ?? new TimeoutException());
    }
}
