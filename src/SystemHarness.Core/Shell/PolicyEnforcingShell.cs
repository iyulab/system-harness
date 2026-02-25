namespace SystemHarness;

/// <summary>
/// Decorator that enforces a <see cref="CommandPolicy"/> before delegating to an inner <see cref="IShell"/>.
/// </summary>
public sealed class PolicyEnforcingShell : IShell
{
    private readonly IShell _inner;
    private readonly CommandPolicy _policy;

    public PolicyEnforcingShell(IShell inner, CommandPolicy policy)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public Task<ShellResult> RunAsync(string command, ShellOptions? options = null, CancellationToken ct = default)
    {
        // Extract the first token (program name) to check against blocked programs,
        // then also check the full "cmd.exe /C {command}" form for pattern matching.
        var firstToken = command.Split(' ', 2)[0];
        var violation = _policy.CheckViolation(firstToken, command.Length > firstToken.Length ? command[(firstToken.Length + 1)..] : string.Empty)
                     ?? _policy.CheckViolation("cmd.exe", $"/C {command}");
        if (violation is not null)
            throw new CommandPolicyException(violation) { BlockedCommand = command };

        return _inner.RunAsync(command, options, ct);
    }

    public Task<ShellResult> RunAsync(string program, string arguments, ShellOptions? options = null, CancellationToken ct = default)
    {
        var violation = _policy.CheckViolation(program, arguments);
        if (violation is not null)
            throw new CommandPolicyException(violation) { BlockedCommand = $"{program} {arguments}" };

        return _inner.RunAsync(program, arguments, options, ct);
    }
}
