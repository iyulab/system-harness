namespace SystemHarness;

/// <summary>
/// Decorator that enforces a <see cref="CommandPolicy"/> before an inner <see cref="IProcessManager"/> starts a process.
/// Starting a program directly is the same act as running it from a shell, so the shell's policy applies here too.
/// Every other member delegates unchanged.
/// </summary>
public sealed class PolicyEnforcingProcessManager : IProcessManager, IDisposable
{
    private readonly IProcessManager _inner;
    private readonly CommandPolicy _policy;

    public PolicyEnforcingProcessManager(IProcessManager inner, CommandPolicy policy)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public Task<ProcessInfo> StartAsync(string path, string? arguments = null, CancellationToken ct = default)
    {
        Enforce(path, arguments);
        return _inner.StartAsync(path, arguments, ct);
    }

    public Task<ProcessInfo> StartAsync(string path, ProcessStartOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        Enforce(path, options.Arguments);
        return _inner.StartAsync(path, options, ct);
    }

    public Task KillAsync(int pid, CancellationToken ct = default) => _inner.KillAsync(pid, ct);

    public Task KillByNameAsync(string name, CancellationToken ct = default) => _inner.KillByNameAsync(name, ct);

    public Task<IReadOnlyList<ProcessInfo>> ListAsync(string? filter = null, CancellationToken ct = default)
        => _inner.ListAsync(filter, ct);

    public Task<bool> IsRunningAsync(string name, CancellationToken ct = default) => _inner.IsRunningAsync(name, ct);

    public Task<IReadOnlyList<ProcessInfo>> FindByPortAsync(int port, CancellationToken ct = default)
        => _inner.FindByPortAsync(port, ct);

    public Task<IReadOnlyList<ProcessInfo>> FindByPathAsync(string executablePath, CancellationToken ct = default)
        => _inner.FindByPathAsync(executablePath, ct);

    public Task<IReadOnlyList<ProcessInfo>> FindByWindowTitleAsync(string titleSubstring, CancellationToken ct = default)
        => _inner.FindByWindowTitleAsync(titleSubstring, ct);

    public Task<IReadOnlyList<ProcessInfo>> GetChildProcessesAsync(int pid, CancellationToken ct = default)
        => _inner.GetChildProcessesAsync(pid, ct);

    public Task KillTreeAsync(int pid, CancellationToken ct = default) => _inner.KillTreeAsync(pid, ct);

    public Task<bool> WaitForExitAsync(int pid, TimeSpan? timeout = null, CancellationToken ct = default)
        => _inner.WaitForExitAsync(pid, timeout, ct);

    public void Dispose() => (_inner as IDisposable)?.Dispose();

    private void Enforce(string path, string? arguments)
    {
        var violation = _policy.CheckViolation(path, arguments);
        if (violation is not null)
            throw new CommandPolicyException(violation) { BlockedCommand = $"{path} {arguments}".TrimEnd() };
    }
}
