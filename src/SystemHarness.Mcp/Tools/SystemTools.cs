using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class SystemTools(IHarness harness)
{
    [McpServerTool(Name = "system_get_env"), Description("Get the value of an environment variable.")]
    public async Task<string> GetEnvAsync(
        [Description("Environment variable name.")] string name, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(name))
            return McpResponse.Error("invalid_parameter", "name cannot be empty.", sw.ElapsedMilliseconds);
        var value = await harness.SystemInfo.GetEnvironmentVariableAsync(name, ct);
        return value is not null
            ? McpResponse.Ok(new { name, value }, sw.ElapsedMilliseconds)
            : McpResponse.Error("not_set", $"Environment variable '{name}' is not set.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "system_set_env"), Description("Set an environment variable for the current process. Pass empty value to unset.")]
    public async Task<string> SetEnvAsync(
        [Description("Environment variable name.")] string name,
        [Description("Value to set. Pass empty string to unset.")] string value,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(name))
            return McpResponse.Error("invalid_parameter", "name cannot be empty.", sw.ElapsedMilliseconds);
        var v = string.IsNullOrEmpty(value) ? null : value;
        await harness.SystemInfo.SetEnvironmentVariableAsync(name, v, ct);
        ActionLog.Record("system_set_env", $"name={name}, value={v ?? "(unset)"}", sw.ElapsedMilliseconds, true);
        return v is null
            ? McpResponse.Confirm($"Unset '{name}'.", sw.ElapsedMilliseconds)
            : McpResponse.Confirm($"Set '{name}' = '{v}'.", sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "system_list_env"), Description("List all environment variables for the current process.")]
    public async Task<string> ListEnvAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var vars = await harness.SystemInfo.GetAllEnvironmentVariablesAsync(ct);
        return McpResponse.Items(vars.Select(kv => new { kv.Key, kv.Value }).ToArray(), sw.ElapsedMilliseconds);
    }

    [McpServerTool(Name = "system_get_info"), Description("Get basic system information (machine name, user, OS version).")]
    public async Task<string> InfoAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var machine = await harness.SystemInfo.GetMachineNameAsync(ct);
        var user = await harness.SystemInfo.GetUserNameAsync(ct);
        var os = await harness.SystemInfo.GetOSVersionAsync(ct);
        return McpResponse.Ok(new { machine, user, os }, sw.ElapsedMilliseconds);
    }
}
