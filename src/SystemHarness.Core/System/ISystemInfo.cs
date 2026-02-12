namespace SystemHarness;

/// <summary>
/// System information and environment variable access.
/// </summary>
public interface ISystemInfo
{
    /// <summary>
    /// Gets the value of an environment variable.
    /// </summary>
    Task<string?> GetEnvironmentVariableAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Sets the value of an environment variable for the current process.
    /// </summary>
    Task SetEnvironmentVariableAsync(string name, string? value, CancellationToken ct = default);

    /// <summary>
    /// Gets the local machine name.
    /// </summary>
    Task<string> GetMachineNameAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current user name.
    /// </summary>
    Task<string> GetUserNameAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a human-readable OS version string.
    /// </summary>
    Task<string> GetOSVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all environment variables as a dictionary.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetAllEnvironmentVariablesAsync(CancellationToken ct = default);
}
