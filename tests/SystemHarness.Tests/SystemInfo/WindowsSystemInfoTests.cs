using SystemHarness.Windows;

namespace SystemHarness.Tests.SystemInfo;

[Trait("Category", "CI")]
public class WindowsSystemInfoTests
{
    private readonly WindowsSystemInfo _systemInfo = new();

    [Fact]
    public async Task GetMachineNameAsync_ReturnsNonEmpty()
    {
        var name = await _systemInfo.GetMachineNameAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(name);
        Assert.NotEmpty(name);
    }

    [Fact]
    public async Task GetUserNameAsync_ReturnsNonEmpty()
    {
        var name = await _systemInfo.GetUserNameAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(name);
        Assert.NotEmpty(name);
    }

    [Fact]
    public async Task GetOSVersionAsync_ContainsWindows()
    {
        var version = await _systemInfo.GetOSVersionAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(version);
        Assert.Contains("Windows", version, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetEnvironmentVariableAsync_ReturnsPath()
    {
        var path = await _systemInfo.GetEnvironmentVariableAsync("PATH", TestContext.Current.CancellationToken);

        Assert.NotNull(path);
        Assert.NotEmpty(path);
    }

    [Fact]
    public async Task GetEnvironmentVariableAsync_ReturnsNullForMissing()
    {
        var value = await _systemInfo.GetEnvironmentVariableAsync("NONEXISTENT_VAR_12345", TestContext.Current.CancellationToken);

        Assert.Null(value);
    }

    [Fact]
    public async Task SetAndGetEnvironmentVariableAsync_RoundTrip()
    {
        var varName = $"TEST_HARNESS_{Guid.NewGuid():N}";
        var varValue = "test_value_42";

        try
        {
            await _systemInfo.SetEnvironmentVariableAsync(varName, varValue, TestContext.Current.CancellationToken);
            var result = await _systemInfo.GetEnvironmentVariableAsync(varName, TestContext.Current.CancellationToken);

            Assert.Equal(varValue, result);
        }
        finally
        {
            await _systemInfo.SetEnvironmentVariableAsync(varName, null, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task GetAllEnvironmentVariablesAsync_ReturnsMultiple()
    {
        var vars = await _systemInfo.GetAllEnvironmentVariablesAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(vars);
        Assert.True(vars.ContainsKey("PATH") || vars.ContainsKey("Path"));
    }
}
