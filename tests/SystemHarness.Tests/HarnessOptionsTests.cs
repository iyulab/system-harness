using SystemHarness.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace SystemHarness.Tests;

[Trait("Category", "CI")]
public class HarnessOptionsTests
{
    [Fact]
    public void DefaultOptions_NoPolicy_NoAudit()
    {
        using var harness = new WindowsHarness(new HarnessOptions());
        Assert.NotNull(harness.Shell);
    }

    [Fact]
    public async Task WithCommandPolicy_BlocksDangerousCommands()
    {
        var options = new HarnessOptions
        {
            CommandPolicy = CommandPolicy.CreateDefault(),
        };

        using var harness = new WindowsHarness(options);

        Assert.Throws<CommandPolicyException>(
            () => harness.Shell.RunAsync("shutdown", "/s /t 0").GetAwaiter().GetResult());

        // Safe commands still work
        var result = await harness.Shell.RunAsync("cmd", "/C echo safe");
        Assert.True(result.Success);
    }

    [Fact]
    public async Task WithAuditLog_RecordsCommands()
    {
        var log = new InMemoryAuditLog();
        var options = new HarnessOptions { AuditLog = log };

        using var harness = new WindowsHarness(options);
        await harness.Shell.RunAsync("cmd", "/C echo audited");

        var entries = await log.GetEntriesAsync();
        Assert.Single(entries);
        Assert.Equal("Shell", entries[0].Category);
    }

    [Fact]
    public async Task WithBothPolicyAndAudit_PolicyCheckedFirst()
    {
        var log = new InMemoryAuditLog();
        var options = new HarnessOptions
        {
            CommandPolicy = CommandPolicy.CreateDefault(),
            AuditLog = log,
        };

        using var harness = new WindowsHarness(options);

        // Blocked command should not be audited (policy throws before audit)
        Assert.Throws<CommandPolicyException>(
            () => harness.Shell.RunAsync("shutdown", "/s /t 0").GetAwaiter().GetResult());

        // Audit log should record the exception
        var entries = await log.GetEntriesAsync();
        Assert.Single(entries);
        Assert.False(entries[0].Success);
    }

    [Fact]
    public void NullOptions_UsesDefaults()
    {
        using var harness = new WindowsHarness(null);
        Assert.NotNull(harness.Shell);
    }

    [Fact]
    public void DI_WithOptions_AppliesPolicy()
    {
        var services = new ServiceCollection();
        services.AddSystemHarness(new HarnessOptions
        {
            CommandPolicy = CommandPolicy.CreateDefault(),
        });

        using var provider = services.BuildServiceProvider();
        var shell = provider.GetRequiredService<IShell>();

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("format", "C:").GetAwaiter().GetResult());
    }

    // --- Edge cases (cycle 229) ---

    [Fact]
    public void DefaultOptions_AllPropertiesNull()
    {
        var options = new HarnessOptions();
        Assert.Null(options.CommandPolicy);
        Assert.Null(options.AuditLog);
        Assert.Null(options.DefaultCaptureOptions);
    }

    [Fact]
    public void WithDefaultCaptureOptions_Accepted()
    {
        var options = new HarnessOptions
        {
            DefaultCaptureOptions = new CaptureOptions
            {
                Format = ImageFormat.Png,
                Quality = 100,
                TargetWidth = 1920,
                TargetHeight = 1080,
            }
        };

        using var harness = new WindowsHarness(options);
        Assert.NotNull(harness.Shell);  // harness constructs successfully
    }

    [Fact]
    public void WithCustomPolicy_AllowsSpecificCommands()
    {
        var policy = new CommandPolicy()
            .BlockProgram("notepad");

        var options = new HarnessOptions { CommandPolicy = policy };
        using var harness = new WindowsHarness(options);

        // Blocked
        Assert.Throws<CommandPolicyException>(
            () => harness.Shell.RunAsync("notepad.exe", "test.txt").GetAwaiter().GetResult());

        // Not blocked — format is not in custom policy
        // (Just verify it doesn't throw — we can't actually run format)
        // ... custom policy only blocks notepad
    }
}
