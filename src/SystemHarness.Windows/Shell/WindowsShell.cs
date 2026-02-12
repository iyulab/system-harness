using System.Diagnostics;
using System.Text;

namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IShell"/> using cmd.exe and powershell.
/// </summary>
public sealed class WindowsShell : IShell
{
    public async Task<ShellResult> RunAsync(string command, ShellOptions? options = null, CancellationToken ct = default)
    {
        // Default to cmd.exe /C for single-string commands
        return await RunAsync("cmd.exe", $"/C {command}", options, ct);
    }

    public async Task<ShellResult> RunAsync(string program, string arguments, ShellOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ShellOptions();

        var startInfo = new ProcessStartInfo
        {
            FileName = program,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (options.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        if (options.EnvironmentVariables is not null)
        {
            foreach (var (key, value) in options.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        var sw = Stopwatch.StartNew();

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };

        // Use StringBuilder to capture output asynchronously
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdoutBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderrBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait with timeout and cancellation — link method CT and options CT
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, options.CancellationToken);
        if (options.Timeout.HasValue)
        {
            cts.CancelAfter(options.Timeout.Value);
        }

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort kill
            }

            sw.Stop();
            return new ShellResult
            {
                ExitCode = -1,
                StdOut = stdoutBuilder.ToString(),
                StdErr = "Process was cancelled or timed out.",
                Elapsed = sw.Elapsed,
            };
        }

        sw.Stop();

        var stdout = stdoutBuilder.ToString();
        var originalByteCount = Encoding.UTF8.GetByteCount(stdout);
        var wasTruncated = false;

        if (options.MaxOutputChars.HasValue && stdout.Length > options.MaxOutputChars.Value)
        {
            stdout = string.Concat(
                stdout.AsSpan(0, options.MaxOutputChars.Value),
                $"\n... [truncated {originalByteCount - options.MaxOutputChars.Value} chars]");
            wasTruncated = true;
        }

        return new ShellResult
        {
            ExitCode = process.ExitCode,
            StdOut = stdout,
            StdErr = stderrBuilder.ToString(),
            Elapsed = sw.Elapsed,
            WasTruncated = wasTruncated,
            OriginalByteCount = originalByteCount,
        };
    }
}
