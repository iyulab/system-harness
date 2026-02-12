using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace SystemHarness.Mcp.Tools;

public sealed class ShellTools(IHarness harness)
{
    [McpServerTool(Name = "shell_execute"), Description("Execute a shell command and return stdout, stderr, and exit code.")]
    public async Task<string> ExecuteAsync(
        [Description("Shell command to execute.")] string command,
        [Description("Working directory for the command. Uses current directory if omitted.")] string? workingDirectory = null,
        [Description("Maximum execution time in milliseconds. No timeout if omitted.")] int? timeoutMs = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(command))
            return McpResponse.Error("invalid_parameter", "command cannot be empty.", sw.ElapsedMilliseconds);
        if (timeoutMs.HasValue && timeoutMs.Value < 0)
            return McpResponse.Error("invalid_timeout", $"timeoutMs cannot be negative (got {timeoutMs}).", sw.ElapsedMilliseconds);
        var options = new ShellOptions();
        if (workingDirectory is not null) options.WorkingDirectory = workingDirectory;
        if (timeoutMs.HasValue) options.Timeout = TimeSpan.FromMilliseconds(timeoutMs.Value);
        options.CancellationToken = ct;

        var result = await harness.Shell.RunAsync(command, options, ct);
        ActionLog.Record("shell_execute", $"cmd={command}", sw.ElapsedMilliseconds, result.ExitCode == 0);
        return McpResponse.Ok(new
        {
            exitCode = result.ExitCode,
            stdout = result.StdOut,
            stderr = result.StdErr,
        }, sw.ElapsedMilliseconds);
    }
}
