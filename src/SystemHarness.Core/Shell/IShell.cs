namespace SystemHarness;

/// <summary>
/// Executes shell commands (cmd, powershell, bash) and returns structured results.
/// </summary>
public interface IShell
{
    /// <summary>
    /// Runs a command string using the platform's default shell.
    /// </summary>
    Task<ShellResult> RunAsync(string command, ShellOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Runs a specific program with arguments.
    /// </summary>
    Task<ShellResult> RunAsync(string program, string arguments, ShellOptions? options = null, CancellationToken ct = default);
}
