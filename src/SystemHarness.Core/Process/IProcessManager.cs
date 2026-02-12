namespace SystemHarness;

/// <summary>
/// Manages operating system processes — start, kill, enumerate, search.
/// </summary>
public interface IProcessManager
{
    /// <summary>
    /// Starts a new process and returns its information.
    /// </summary>
    Task<ProcessInfo> StartAsync(string path, string? arguments = null, CancellationToken ct = default);

    /// <summary>
    /// Kills a process by its PID.
    /// </summary>
    Task KillAsync(int pid, CancellationToken ct = default);

    /// <summary>
    /// Kills all processes matching the given name.
    /// </summary>
    Task KillByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Lists running processes, optionally filtered by name.
    /// </summary>
    Task<IReadOnlyList<ProcessInfo>> ListAsync(string? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Checks whether any process with the given name is currently running.
    /// </summary>
    Task<bool> IsRunningAsync(string name, CancellationToken ct = default);

    // --- Phase 7 Extensions (DIM for backward compatibility) ---

    /// <summary>
    /// Starts a new process with detailed options.
    /// </summary>
    Task<ProcessInfo> StartAsync(string path, ProcessStartOptions options, CancellationToken ct = default)
        => throw new NotSupportedException("StartAsync with options is not supported by this implementation.");

    /// <summary>
    /// Finds processes listening on the specified network port.
    /// </summary>
    Task<IReadOnlyList<ProcessInfo>> FindByPortAsync(int port, CancellationToken ct = default)
        => throw new NotSupportedException("FindByPortAsync is not supported by this implementation.");

    /// <summary>
    /// Finds processes by their executable path.
    /// </summary>
    Task<IReadOnlyList<ProcessInfo>> FindByPathAsync(string executablePath, CancellationToken ct = default)
        => throw new NotSupportedException("FindByPathAsync is not supported by this implementation.");

    /// <summary>
    /// Finds processes that have a window matching the given title substring.
    /// </summary>
    Task<IReadOnlyList<ProcessInfo>> FindByWindowTitleAsync(string titleSubstring, CancellationToken ct = default)
        => throw new NotSupportedException("FindByWindowTitleAsync is not supported by this implementation.");

    /// <summary>
    /// Gets all child processes of the given PID.
    /// </summary>
    Task<IReadOnlyList<ProcessInfo>> GetChildProcessesAsync(int pid, CancellationToken ct = default)
        => throw new NotSupportedException("GetChildProcessesAsync is not supported by this implementation.");

    /// <summary>
    /// Kills a process and all its descendant processes (process tree kill).
    /// </summary>
    Task KillTreeAsync(int pid, CancellationToken ct = default)
        => throw new NotSupportedException("KillTreeAsync is not supported by this implementation.");

    /// <summary>
    /// Waits for a process to exit, with optional timeout.
    /// Returns true if the process exited, false if timeout elapsed.
    /// </summary>
    Task<bool> WaitForExitAsync(int pid, TimeSpan? timeout = null, CancellationToken ct = default)
        => throw new NotSupportedException("WaitForExitAsync is not supported by this implementation.");
}
