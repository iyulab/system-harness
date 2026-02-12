namespace SystemHarness;

/// <summary>
/// Options for shell command execution.
/// </summary>
public sealed class ShellOptions
{
    /// <summary>
    /// Working directory for the command. Null uses the current directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Additional environment variables to set for the process.
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; set; }

    /// <summary>
    /// Maximum time to wait for the command to complete.
    /// Null means no timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Cancellation token for cooperative cancellation.
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Maximum number of characters to capture from StdOut.
    /// Output exceeding this limit will be truncated.
    /// Null means no limit.
    /// </summary>
    public int? MaxOutputChars { get; set; }
}
