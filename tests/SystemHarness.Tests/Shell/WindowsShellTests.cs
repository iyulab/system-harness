using SystemHarness.Windows;

namespace SystemHarness.Tests.Shell;

[Trait("Category", "CI")]
public class WindowsShellTests
{
    private readonly WindowsShell _shell = new();

    [Fact]
    public async Task RunAsync_SimpleCommand_ReturnsOutput()
    {
        var result = await _shell.RunAsync("echo hello");

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StdOut);
    }

    [Fact]
    public async Task RunAsync_ProgramAndArgs_ReturnsOutput()
    {
        var result = await _shell.RunAsync("cmd.exe", "/C echo world");

        Assert.True(result.Success);
        Assert.Contains("world", result.StdOut);
    }

    [Fact]
    public async Task RunAsync_FailingCommand_ReturnsNonZeroExitCode()
    {
        var result = await _shell.RunAsync("cmd.exe", "/C exit 42");

        Assert.False(result.Success);
        Assert.Equal(42, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_StdErr_IsCaptured()
    {
        var result = await _shell.RunAsync("cmd.exe", "/C echo error>&2");

        Assert.Contains("error", result.StdErr);
    }

    [Fact]
    public async Task RunAsync_Timeout_KillsProcess()
    {
        var options = new ShellOptions { Timeout = TimeSpan.FromMilliseconds(500) };

        var result = await _shell.RunAsync("cmd.exe", "/C ping -n 30 127.0.0.1", options);

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("cancelled", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_CancellationToken_KillsProcess()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var options = new ShellOptions { CancellationToken = cts.Token };

        var result = await _shell.RunAsync("cmd.exe", "/C ping -n 30 127.0.0.1", options);

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_Truncation_TruncatesLongOutput()
    {
        var options = new ShellOptions { MaxOutputChars = 20 };

        var result = await _shell.RunAsync("cmd.exe", "/C echo AAAAAAAAAAAAAAAAAAAABBBBBBBBBBBBBBBBBBBB", options);

        Assert.True(result.WasTruncated);
        Assert.Contains("[truncated", result.StdOut);
        Assert.True(result.OriginalByteCount > 20);
    }

    [Fact]
    public async Task RunAsync_WorkingDirectory_IsRespected()
    {
        var options = new ShellOptions { WorkingDirectory = "C:\\" };

        var result = await _shell.RunAsync("cmd.exe", "/C cd", options);

        Assert.True(result.Success);
        Assert.Contains("C:\\", result.StdOut);
    }

    [Fact]
    public async Task RunAsync_EnvironmentVariables_AreSet()
    {
        var options = new ShellOptions
        {
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["TEST_HARNESS_VAR"] = "harness_value"
            }
        };

        var result = await _shell.RunAsync("cmd.exe", "/C echo %TEST_HARNESS_VAR%", options);

        Assert.True(result.Success);
        Assert.Contains("harness_value", result.StdOut);
    }

    [Fact]
    public async Task RunAsync_Elapsed_IsPopulated()
    {
        var result = await _shell.RunAsync("echo fast");

        Assert.True(result.Elapsed > TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_PowerShell_Works()
    {
        var result = await _shell.RunAsync("powershell.exe", "-NoProfile -Command Write-Output 'pwsh works'");

        Assert.True(result.Success);
        Assert.Contains("pwsh works", result.StdOut);
    }

    [Fact]
    public async Task RunAsync_UnicodeOutput_PreservesCharacters()
    {
        var result = await _shell.RunAsync("powershell.exe",
            "-NoProfile -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Write-Output 'café résumé naïve'\"");

        Assert.True(result.Success);
        Assert.Contains("café", result.StdOut);
    }
}
