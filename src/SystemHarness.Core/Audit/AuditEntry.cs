namespace SystemHarness;

/// <summary>
/// A single audit log entry recording one harness action.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>
    /// When the action started.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Category of the action (e.g., "Shell", "Mouse", "Keyboard", "Screen").
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Specific action performed (e.g., "RunAsync", "ClickAsync", "CaptureAsync").
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Human-readable details of the action (e.g., command text, coordinates).
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// How long the action took to complete.
    /// </summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// Whether the action succeeded.
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if the action failed.
    /// </summary>
    public string? Error { get; init; }
}
