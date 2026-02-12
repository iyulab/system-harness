namespace SystemHarness;

/// <summary>
/// Information about a running or launched process.
/// </summary>
public sealed class ProcessInfo
{
    /// <summary>
    /// Process identifier.
    /// </summary>
    public required int Pid { get; init; }

    /// <summary>
    /// Process name (without extension on Windows).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path to the process executable, if available.
    /// </summary>
    public string? ExecutablePath { get; init; }

    /// <summary>
    /// Title of the process's main window, if any.
    /// </summary>
    public string? MainWindowTitle { get; init; }

    /// <summary>
    /// Whether the process is still running.
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// Process start time, if available.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// Parent process ID, if available.
    /// </summary>
    public int? ParentPid { get; init; }

    /// <summary>
    /// Full command line used to launch the process, if available.
    /// </summary>
    public string? CommandLine { get; init; }

    /// <summary>
    /// Working set memory usage in bytes, if available.
    /// </summary>
    public long? MemoryUsageBytes { get; init; }

    /// <summary>
    /// CPU usage percentage (0-100), if available. Snapshot at query time.
    /// </summary>
    public double? CpuUsagePercent { get; init; }
}
