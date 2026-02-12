namespace SystemHarness;

/// <summary>
/// Options for starting a new process with fine-grained control.
/// </summary>
public sealed class ProcessStartOptions
{
    /// <summary>
    /// Working directory for the new process. Null uses the current directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Additional environment variables to set for the process.
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; set; }

    /// <summary>
    /// Whether to start the process with elevated (administrator) privileges.
    /// On Windows, this triggers a UAC prompt if needed.
    /// </summary>
    public bool RunElevated { get; set; }

    /// <summary>
    /// Whether to start the process with a hidden window.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// Whether to redirect standard output and error streams.
    /// When true, the process runs without a visible console window.
    /// </summary>
    public bool RedirectOutput { get; set; }

    /// <summary>
    /// Arguments to pass to the process.
    /// </summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// Maximum time to wait for the process to complete (when used with output capture).
    /// Null means no timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Maximum number of characters to capture from stdout.
    /// Null means no limit.
    /// </summary>
    public int? MaxOutputChars { get; set; }
}
