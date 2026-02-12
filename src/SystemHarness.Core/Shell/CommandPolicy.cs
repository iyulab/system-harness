using System.Text.RegularExpressions;

namespace SystemHarness;

/// <summary>
/// Defines which shell commands are allowed or blocked.
/// Commands are checked against blocked patterns before execution.
/// </summary>
public sealed class CommandPolicy
{
    private readonly object _lock = new();
    private readonly List<Regex> _blockedPatterns = [];
    private readonly HashSet<string> _blockedPrograms = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates an empty policy (all commands allowed).
    /// </summary>
    public CommandPolicy() { }

    /// <summary>
    /// Blocks commands matching the given regex pattern.
    /// </summary>
    public CommandPolicy BlockPattern(string pattern)
    {
        lock (_lock)
            _blockedPatterns.Add(new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled));
        return this;
    }

    /// <summary>
    /// Blocks a specific program from being executed.
    /// </summary>
    public CommandPolicy BlockProgram(string program)
    {
        lock (_lock)
            _blockedPrograms.Add(program);
        return this;
    }

    /// <summary>
    /// Returns a pre-built policy blocking common destructive operations.
    /// Blocks: format, mkfs, rm -rf, del /s, shutdown, reboot, diskpart,
    /// registry deletion, and recursive force-delete patterns.
    /// </summary>
    public static CommandPolicy CreateDefault()
    {
        return new CommandPolicy()
            // Destructive disk/partition commands
            .BlockProgram("format")
            .BlockProgram("mkfs")
            .BlockProgram("diskpart")
            .BlockProgram("fdisk")
            // System shutdown/reboot
            .BlockProgram("shutdown")
            .BlockProgram("reboot")
            // Dangerous patterns
            .BlockPattern(@"rm\s+(-\w*f\w*\s+)*-\w*r|rm\s+(-\w*r\w*\s+)*-\w*f")  // rm -rf variants
            .BlockPattern(@"rm\s+-rf\s+/\s*$")           // rm -rf /
            .BlockPattern(@"del\s+/[sS]")                // del /s (recursive delete)
            .BlockPattern(@"rd\s+/[sS]\s+/[qQ]")         // rd /s /q (recursive remove dir)
            .BlockPattern(@"reg\s+delete")                // registry deletion
            .BlockPattern(@":\(\)\{.*\|.*\};:")           // fork bomb
            .BlockPattern(@">\s*/dev/sd[a-z]")            // write to raw disk
            .BlockPattern(@"dd\s+.*of=/dev/")             // dd to device
            .BlockPattern(@"mkfs\.");                     // filesystem format
    }

    /// <summary>
    /// Checks whether the given command and program are allowed by this policy.
    /// </summary>
    /// <returns>Null if allowed; violation message if blocked.</returns>
    internal string? CheckViolation(string program, string arguments)
    {
        var programName = Path.GetFileNameWithoutExtension(program);

        lock (_lock)
        {
            if (_blockedPrograms.Contains(programName))
                return $"Program '{programName}' is blocked by command policy.";

            var fullCommand = $"{program} {arguments}";

            foreach (var pattern in _blockedPatterns)
            {
                if (pattern.IsMatch(fullCommand))
                    return $"Command matches blocked pattern: {pattern}";
            }
        }

        return null;
    }
}
