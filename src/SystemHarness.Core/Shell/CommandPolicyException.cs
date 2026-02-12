namespace SystemHarness;

/// <summary>
/// Thrown when a shell command violates the configured <see cref="CommandPolicy"/>.
/// </summary>
public sealed class CommandPolicyException : HarnessException
{
    public CommandPolicyException(string message) : base(message) { }

    /// <summary>
    /// The command that was blocked.
    /// </summary>
    public string? BlockedCommand { get; init; }
}
