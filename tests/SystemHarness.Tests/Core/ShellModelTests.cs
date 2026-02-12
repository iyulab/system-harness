namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class ShellModelTests
{
    // --- ShellOptions ---

    [Fact]
    public void ShellOptions_Defaults_AllNull()
    {
        var options = new ShellOptions();
        Assert.Null(options.WorkingDirectory);
        Assert.Null(options.EnvironmentVariables);
        Assert.Null(options.Timeout);
        Assert.Null(options.MaxOutputChars);
        Assert.Equal(CancellationToken.None, options.CancellationToken);
    }

    [Fact]
    public void ShellOptions_CustomValues()
    {
        var cts = new CancellationTokenSource();
        var options = new ShellOptions
        {
            WorkingDirectory = @"C:\temp",
            EnvironmentVariables = new() { ["PATH"] = "/usr/bin" },
            Timeout = TimeSpan.FromSeconds(30),
            MaxOutputChars = 10000,
            CancellationToken = cts.Token,
        };

        Assert.Equal(@"C:\temp", options.WorkingDirectory);
        Assert.Single(options.EnvironmentVariables!);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        Assert.Equal(10000, options.MaxOutputChars);
        Assert.Equal(cts.Token, options.CancellationToken);
    }

    // --- ShellResult ---

    [Fact]
    public void ShellResult_Success_WhenExitCodeZero()
    {
        var result = new ShellResult { ExitCode = 0, StdOut = "", StdErr = "", Elapsed = TimeSpan.Zero };
        Assert.True(result.Success);
    }

    [Fact]
    public void ShellResult_NotSuccess_WhenExitCodeNonZero()
    {
        var result = new ShellResult { ExitCode = 1, StdOut = "", StdErr = "error", Elapsed = TimeSpan.Zero };
        Assert.False(result.Success);
    }

    [Fact]
    public void ShellResult_NegativeExitCode_NotSuccess()
    {
        var result = new ShellResult { ExitCode = -1, StdOut = "", StdErr = "", Elapsed = TimeSpan.Zero };
        Assert.False(result.Success);
    }

    [Fact]
    public void ShellResult_TruncationFields_DefaultFalse()
    {
        var result = new ShellResult { ExitCode = 0, StdOut = "data", StdErr = "", Elapsed = TimeSpan.FromMilliseconds(50) };
        Assert.False(result.WasTruncated);
        Assert.Equal(0, result.OriginalByteCount);
    }

    [Fact]
    public void ShellResult_TruncationFields_WhenSet()
    {
        var result = new ShellResult
        {
            ExitCode = 0,
            StdOut = "truncated...",
            StdErr = "",
            Elapsed = TimeSpan.FromSeconds(1),
            WasTruncated = true,
            OriginalByteCount = 1_000_000,
        };

        Assert.True(result.WasTruncated);
        Assert.Equal(1_000_000, result.OriginalByteCount);
    }

    [Fact]
    public void ShellResult_ElapsedPreserved()
    {
        var elapsed = TimeSpan.FromMilliseconds(1234);
        var result = new ShellResult { ExitCode = 0, StdOut = "", StdErr = "", Elapsed = elapsed };
        Assert.Equal(1234, result.Elapsed.TotalMilliseconds);
    }
}
