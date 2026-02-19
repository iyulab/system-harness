using System.Diagnostics;

namespace SystemHarness;

/// <summary>
/// Decorator that logs all shell command executions to an <see cref="IAuditLog"/>.
/// </summary>
public sealed class AuditingShell : IShell
{
    private readonly IShell _inner;
    private readonly IAuditLog _log;

    public AuditingShell(IShell inner, IAuditLog log)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<ShellResult> RunAsync(string command, ShellOptions? options = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _inner.RunAsync(command, options, ct);
            sw.Stop();
            await _log.AppendAsync(new AuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Category = "Shell",
                Action = "RunAsync",
                Details = command,
                Duration = sw.Elapsed,
                Success = result.Success,
                Error = result.Success ? null : result.StdErr,
            }, ct);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            await _log.AppendAsync(new AuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Category = "Shell",
                Action = "RunAsync",
                Details = command,
                Duration = sw.Elapsed,
                Success = false,
                Error = ex.Message,
            }, ct);
            throw;
        }
    }

    public async Task<ShellResult> RunAsync(string program, string arguments, ShellOptions? options = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var details = $"{program} {arguments}";
        try
        {
            var result = await _inner.RunAsync(program, arguments, options, ct);
            sw.Stop();
            await _log.AppendAsync(new AuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Category = "Shell",
                Action = "RunAsync",
                Details = details,
                Duration = sw.Elapsed,
                Success = result.Success,
                Error = result.Success ? null : result.StdErr,
            }, ct);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            await _log.AppendAsync(new AuditEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Category = "Shell",
                Action = "RunAsync",
                Details = details,
                Duration = sw.Elapsed,
                Success = false,
                Error = ex.Message,
            }, ct);
            throw;
        }
    }
}
