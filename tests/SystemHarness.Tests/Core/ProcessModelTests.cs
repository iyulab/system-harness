namespace SystemHarness.Tests.Core;

[Trait("Category", "CI")]
public class ProcessModelTests
{
    // --- ProcessInfo ---

    [Fact]
    public void ProcessInfo_RequiredProperties()
    {
        var info = new ProcessInfo { Pid = 1234, Name = "notepad" };
        Assert.Equal(1234, info.Pid);
        Assert.Equal("notepad", info.Name);
    }

    [Fact]
    public void ProcessInfo_OptionalProperties_DefaultNull()
    {
        var info = new ProcessInfo { Pid = 1, Name = "test" };
        Assert.Null(info.ExecutablePath);
        Assert.Null(info.MainWindowTitle);
        Assert.Null(info.StartTime);
        Assert.Null(info.ParentPid);
        Assert.Null(info.CommandLine);
        Assert.Null(info.MemoryUsageBytes);
        Assert.Null(info.CpuUsagePercent);
        Assert.False(info.IsRunning);
    }

    [Fact]
    public void ProcessInfo_FullyPopulated()
    {
        var startTime = DateTimeOffset.UtcNow;
        var info = new ProcessInfo
        {
            Pid = 5678,
            Name = "chrome",
            ExecutablePath = @"C:\Program Files\Chrome\chrome.exe",
            MainWindowTitle = "Google Chrome",
            IsRunning = true,
            StartTime = startTime,
            ParentPid = 1000,
            CommandLine = "chrome.exe --no-sandbox",
            MemoryUsageBytes = 500_000_000,
            CpuUsagePercent = 12.5,
        };

        Assert.Equal(5678, info.Pid);
        Assert.Equal("chrome", info.Name);
        Assert.Equal(@"C:\Program Files\Chrome\chrome.exe", info.ExecutablePath);
        Assert.Equal("Google Chrome", info.MainWindowTitle);
        Assert.True(info.IsRunning);
        Assert.Equal(startTime, info.StartTime);
        Assert.Equal(1000, info.ParentPid);
        Assert.Equal("chrome.exe --no-sandbox", info.CommandLine);
        Assert.Equal(500_000_000, info.MemoryUsageBytes);
        Assert.Equal(12.5, info.CpuUsagePercent);
    }

    // --- ProcessStartOptions ---

    [Fact]
    public void ProcessStartOptions_Defaults()
    {
        var options = new ProcessStartOptions();
        Assert.Null(options.WorkingDirectory);
        Assert.Null(options.EnvironmentVariables);
        Assert.Null(options.Arguments);
        Assert.Null(options.Timeout);
        Assert.Null(options.MaxOutputChars);
        Assert.False(options.RunElevated);
        Assert.False(options.Hidden);
        Assert.False(options.RedirectOutput);
    }

    [Fact]
    public void ProcessStartOptions_CustomValues()
    {
        var options = new ProcessStartOptions
        {
            WorkingDirectory = @"C:\projects",
            EnvironmentVariables = new() { ["DOTNET_ROOT"] = @"C:\dotnet" },
            Arguments = "--verbose --output results.txt",
            Timeout = TimeSpan.FromMinutes(5),
            MaxOutputChars = 50000,
            RunElevated = true,
            Hidden = true,
            RedirectOutput = true,
        };

        Assert.Equal(@"C:\projects", options.WorkingDirectory);
        Assert.Single(options.EnvironmentVariables!);
        Assert.Equal("--verbose --output results.txt", options.Arguments);
        Assert.Equal(TimeSpan.FromMinutes(5), options.Timeout);
        Assert.Equal(50000, options.MaxOutputChars);
        Assert.True(options.RunElevated);
        Assert.True(options.Hidden);
        Assert.True(options.RedirectOutput);
    }
}
