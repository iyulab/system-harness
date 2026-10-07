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
    internal string? CheckViolation(string program, string? arguments)
    {
        arguments ??= string.Empty;
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

            // A shell host runs part of its arguments as further commands ("cmd /c dir & shutdown /s"), so every
            // program-like token of that part is checked too. Only that part: the arguments a host hands to a script
            // ("pwsh -File run.ps1 format", "bash run.sh shutdown") are the script's data, never run as commands.
            if (CommandPayload(programName, arguments) is { } payload)
            {
                foreach (var token in payload.Split(CommandSeparators, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token[0] is '/' or '-')
                        continue;

                    var nested = Path.GetFileNameWithoutExtension(token);
                    if (_blockedPrograms.Contains(nested))
                        return $"Program '{nested}' is blocked by command policy.";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The part of a shell host's arguments the host runs as commands, or null when the program is not a shell host or
    /// runs a script file (whose following arguments are data). Unknown shapes return the whole argument string, so an
    /// argument the parser does not understand is still checked.
    /// </summary>
    internal static string? CommandPayload(string programName, string arguments)
    {
        if (programName.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ||
            programName.Equals("powershell", StringComparison.OrdinalIgnoreCase))
        {
            return PowerShellPayload(arguments, bareArgumentIsFile: programName.Equals("pwsh", StringComparison.OrdinalIgnoreCase));
        }

        if (programName.Equals("bash", StringComparison.OrdinalIgnoreCase) ||
            programName.Equals("sh", StringComparison.OrdinalIgnoreCase))
        {
            return PosixShellPayload(arguments);
        }

        // cmd re-parses everything after /c or /k (& | && ||), and wsl runs its arguments as a Linux command line
        return ShellHosts.Contains(programName) ? arguments : null;
    }

    // pwsh/powershell: -Command (-c) and -EncodedCommand (-e/-ec) carry commands; -File (-f) names a script and what
    // follows is its arguments. A first bare argument is a script for pwsh (its default parameter is -File) and a
    // command for Windows PowerShell (default -Command), unless it names a .ps1 file.
    private static string? PowerShellPayload(string arguments, bool bareArgumentIsFile)
    {
        var tokens = Tokenize(arguments);
        for (var i = 0; i < tokens.Count; i++)
        {
            var (text, start) = tokens[i];
            if (text.Length > 1 && text[0] is '-' or '/')
            {
                var name = text[1..].TrimEnd(':');
                if (IsAbbreviation(name, "File", 1))
                    return null;

                if (IsAbbreviation(name, "EncodedCommand", 1) || name.Equals("ec", StringComparison.OrdinalIgnoreCase))
                    return i + 1 < tokens.Count ? DecodeOrRaw(tokens[i + 1].Text) : string.Empty;

                if (IsAbbreviation(name, "Command", 1))
                    return arguments[Math.Min(arguments.Length, start + text.Length)..];

                if (PowerShellValueOptions.Any(option => IsAbbreviation(name, option, 2)))
                    i++; // skip the option's value

                continue;
            }

            var isScript = text.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
            return isScript || bareArgumentIsFile ? null : arguments[start..];
        }

        return null;
    }

    // bash/sh: -c carries the command string; otherwise the first non-option argument is a script and the rest its data
    private static string? PosixShellPayload(string arguments)
    {
        var tokens = Tokenize(arguments);
        for (var i = 0; i < tokens.Count; i++)
        {
            var (text, start) = tokens[i];
            if (text.StartsWith('-') || text.StartsWith('+'))
            {
                if (text.Length > 1 && text[0] == '-' && text[1] != '-' && text.Contains('c', StringComparison.Ordinal))
                    return arguments[Math.Min(arguments.Length, start + text.Length)..];

                // -o / +o take an option name ("-o pipefail"), which is not a script
                if (text is "-o" or "+o" or "-O" or "+O")
                    i++;

                continue;
            }

            return null;
        }

        return null;
    }

    private static bool IsAbbreviation(string name, string parameter, int minimumLength) =>
        name.Length >= minimumLength && name.Length <= parameter.Length &&
        parameter.StartsWith(name, StringComparison.OrdinalIgnoreCase);

    private static string DecodeOrRaw(string value)
    {
        try
        {
            return System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return value;
        }
    }

    // Splits on whitespace outside double or single quotes; each token keeps its start offset, quotes stripped
    private static List<(string Text, int Start)> Tokenize(string arguments)
    {
        var tokens = new List<(string, int)>();
        var i = 0;
        while (i < arguments.Length)
        {
            while (i < arguments.Length && char.IsWhiteSpace(arguments[i]))
                i++;
            if (i >= arguments.Length)
                break;

            var start = i;
            var text = new System.Text.StringBuilder();
            char? quote = null;
            for (; i < arguments.Length; i++)
            {
                var c = arguments[i];
                if (quote is null && char.IsWhiteSpace(c))
                    break;
                if (c is '"' or '\'' && (quote is null || quote == c))
                {
                    quote = quote is null ? c : null;
                    continue;
                }

                text.Append(c);
            }

            tokens.Add((text.ToString(), start));
        }

        return tokens;
    }

    // Options that take a value (the token after them is not a script or a command)
    private static readonly string[] PowerShellValueOptions =
    [
        "ExecutionPolicy", "WorkingDirectory", "ConfigurationName", "ConfigurationFile", "OutputFormat", "InputFormat",
        "WindowStyle", "Version", "SettingsFile", "CustomPipeName", "PSConsoleFile", "WorkingDir",
    ];

    private static readonly HashSet<string> ShellHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "powershell", "pwsh", "bash", "sh", "wsl",
    };

    private static readonly char[] CommandSeparators = [' ', '\t', '&', '|', ';', '(', ')', '"', '\''];
}
