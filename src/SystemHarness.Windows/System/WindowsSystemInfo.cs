namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="ISystemInfo"/>.
/// </summary>
public sealed class WindowsSystemInfo : ISystemInfo
{
    public Task<string?> GetEnvironmentVariableAsync(string name, CancellationToken ct = default)
    {
        return Task.FromResult(Environment.GetEnvironmentVariable(name));
    }

    public Task SetEnvironmentVariableAsync(string name, string? value, CancellationToken ct = default)
    {
        Environment.SetEnvironmentVariable(name, value);
        return Task.CompletedTask;
    }

    public Task<string> GetMachineNameAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Environment.MachineName);
    }

    public Task<string> GetUserNameAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Environment.UserName);
    }

    public Task<string> GetOSVersionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(Environment.OSVersion.ToString());
    }

    public Task<IReadOnlyDictionary<string, string>> GetAllEnvironmentVariablesAsync(CancellationToken ct = default)
    {
        var envVars = Environment.GetEnvironmentVariables();
        var dict = new Dictionary<string, string>();

        foreach (System.Collections.DictionaryEntry entry in envVars)
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                dict[key] = value;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(dict);
    }
}
