namespace SystemHarness;

/// <summary>
/// Structured result of a shell command execution.
/// </summary>
public sealed class ShellResult
{
    /// <summary>
    /// Process exit code. 0 typically indicates success.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Captured standard output.
    /// </summary>
    public required string StdOut { get; init; }

    /// <summary>
    /// Captured standard error.
    /// </summary>
    public required string StdErr { get; init; }

    /// <summary>
    /// Wall-clock time elapsed during execution.
    /// </summary>
    public required TimeSpan Elapsed { get; init; }

    /// <summary>
    /// Whether the output was truncated to fit within size limits.
    /// </summary>
    public bool WasTruncated { get; init; }

    /// <summary>
    /// Original byte count of StdOut before truncation.
    /// Only meaningful when <see cref="WasTruncated"/> is true.
    /// </summary>
    public int OriginalByteCount { get; init; }

    /// <summary>
    /// True if the process exited with code 0.
    /// </summary>
    public bool Success => ExitCode == 0;
}
