namespace SystemHarness;

/// <summary>
/// Configuration options for the harness facade.
/// Controls safety features, audit logging, and default behaviors.
/// </summary>
public sealed class HarnessOptions
{
    /// <summary>
    /// Command policy for shell command filtering.
    /// Null means no policy (all commands allowed).
    /// Use <see cref="CommandPolicy.CreateDefault()"/> for standard safety.
    /// </summary>
    public CommandPolicy? CommandPolicy { get; set; }

    /// <summary>
    /// Audit log sink for action recording.
    /// Null means no auditing.
    /// </summary>
    public IAuditLog? AuditLog { get; set; }

    /// <summary>
    /// Default capture options for screen capture operations.
    /// Null uses the built-in defaults.
    /// </summary>
    public CaptureOptions? DefaultCaptureOptions { get; set; }
}
