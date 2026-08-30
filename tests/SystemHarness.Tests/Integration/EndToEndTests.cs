using SystemHarness.Windows;

namespace SystemHarness.Tests.Integration;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
[Trait("Category", "RequiresDesktop")]
public sealed class EndToEndTests : IAsyncLifetime, IDisposable
{
    private WindowsHarness _harness = null!;

    public ValueTask InitializeAsync()
    {
        _harness = new WindowsHarness(new HarnessOptions
        {
            CommandPolicy = CommandPolicy.CreateDefault(),
            AuditLog = new InMemoryAuditLog(),
        });
        return ValueTask.CompletedTask;
    }

    public void Dispose() => _harness?.Dispose();

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ShellToFileSystem_WriteAndReadViaShell()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"e2e-{Guid.NewGuid()}.txt");
        try
        {
            // Write via shell
            await _harness.Shell.RunAsync("cmd", $"/C echo e2e-content > \"{tempFile}\"", ct: TestContext.Current.CancellationToken);

            // Read via filesystem
            var exists = await _harness.FileSystem.ExistsAsync(tempFile, TestContext.Current.CancellationToken);
            Assert.True(exists);

            var content = await _harness.FileSystem.ReadAsync(tempFile, TestContext.Current.CancellationToken);
            Assert.Contains("e2e-content", content);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProcessLifecycle_StartListKill()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();

        // Start notepad
        var proc = await _harness.Process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        try
        {
            // Verify it's running
            var isRunning = await _harness.Process.IsRunningAsync("notepad", TestContext.Current.CancellationToken);
            Assert.True(isRunning);

            // List and find it
            var processes = await _harness.Process.ListAsync("notepad", TestContext.Current.CancellationToken);
            Assert.NotEmpty(processes);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _harness.Process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            await Task.Delay(500, CancellationToken.None);
            #pragma warning restore xUnit1051

            // Verify our specific PID is gone (not "notepad" by name, other tests may have instances)
            try
            {
                var p = System.Diagnostics.Process.GetProcessById(proc.Pid);
                Assert.True(p.HasExited);
                p.Dispose();
            }
            catch (ArgumentException)
            {
                // Process already gone — expected
            }
        }
    }

    [Fact]
    public async Task ScreenCapture_ReturnsValidImage()
    {
        using var screenshot = await _harness.Screen.CaptureAsync(ct: TestContext.Current.CancellationToken);

        Assert.NotNull(screenshot);
        Assert.True(screenshot.Width > 0);
        Assert.True(screenshot.Height > 0);
        Assert.NotEmpty(screenshot.Bytes);
        Assert.NotEmpty(screenshot.Base64);
        Assert.Equal("image/jpeg", screenshot.MimeType);
    }

    [Fact]
    public async Task ScreenCapture_PngFormat()
    {
        using var screenshot = await _harness.Screen.CaptureAsync(new CaptureOptions
        {
            Format = ImageFormat.Png,
            TargetWidth = null,
            TargetHeight = null,
        }, TestContext.Current.CancellationToken);

        Assert.Equal("image/png", screenshot.MimeType);
        // PNG magic bytes: 0x89 P N G
        Assert.Equal(0x89, screenshot.Bytes[0]);
        Assert.Equal((byte)'P', screenshot.Bytes[1]);
    }

    [Fact]
    public async Task ClipboardRoundTrip()
    {
        var testText = $"e2e-clipboard-{Guid.NewGuid()}";

        await _harness.Clipboard.SetTextAsync(testText, TestContext.Current.CancellationToken);
        var result = await _harness.Clipboard.GetTextAsync(TestContext.Current.CancellationToken);

        Assert.Equal(testText, result);
    }

    [Fact]
    public async Task MousePosition_GetAndMove()
    {
        var (origX, origY) = await _harness.Mouse.GetPositionAsync(TestContext.Current.CancellationToken);

        await _harness.Mouse.MoveAsync(100, 100, TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        var (newX, newY) = await _harness.Mouse.GetPositionAsync(TestContext.Current.CancellationToken);
        Assert.InRange(newX, 95, 105);
        Assert.InRange(newY, 95, 105);

        // Restore original position
        await _harness.Mouse.MoveAsync(origX, origY, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FullFlow_LaunchTypeCapture()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();

        // Launch notepad
        var proc = await _harness.Process.StartAsync("notepad.exe", ct: TestContext.Current.CancellationToken);
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            // Focus it
            await _harness.Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Type some text
            await _harness.Keyboard.TypeAsync("Hello from E2E test!", ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Capture screen
            using var screenshot = await _harness.Screen.CaptureAsync(ct: TestContext.Current.CancellationToken);
            Assert.NotNull(screenshot);
            Assert.True(screenshot.Bytes.Length > 0);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            try { await _harness.Process.KillAsync(proc.Pid, CancellationToken.None); } catch { }
            await Task.Delay(300, CancellationToken.None);
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task PolicyBlocksDangerousViaFacade()
    {
        Assert.Throws<CommandPolicyException>(
            () => _harness.Shell.RunAsync("format", "C: /FS:NTFS", ct: TestContext.Current.CancellationToken).GetAwaiter().GetResult());

        // But safe commands work
        var result = await _harness.Shell.RunAsync("cmd", "/C echo safe", ct: TestContext.Current.CancellationToken);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task WindowManagement_ListAndFocus()
    {
        var windows = await _harness.Window.ListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(windows);

        // Find at least one visible window
        var visible = windows.Where(w => w.IsVisible).ToList();
        Assert.NotEmpty(visible);
    }

    [Fact]
    public void HarnessFacade_ExposesAll12Services()
    {
        Assert.NotNull(_harness.Shell);
        Assert.NotNull(_harness.Process);
        Assert.NotNull(_harness.FileSystem);
        Assert.NotNull(_harness.Window);
        Assert.NotNull(_harness.Clipboard);
        Assert.NotNull(_harness.Screen);
        Assert.NotNull(_harness.Mouse);
        Assert.NotNull(_harness.Keyboard);
        Assert.NotNull(_harness.Display);
        Assert.NotNull(_harness.SystemInfo);
        Assert.NotNull(_harness.VirtualDesktop);
        Assert.NotNull(_harness.DialogHandler);
    }

    [Fact]
    public async Task SystemInfo_ThroughHarness()
    {
        var machine = await _harness.SystemInfo.GetMachineNameAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(machine);

        var user = await _harness.SystemInfo.GetUserNameAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(user);
    }

    [Fact]
    public void EmergencyStop_IntegrationWithCancellation()
    {
        var stop = new EmergencyStop();
        Assert.False(stop.IsTriggered);

        stop.Trigger();
        Assert.True(stop.Token.IsCancellationRequested);

        stop.Reset();
        Assert.False(stop.Token.IsCancellationRequested);

        stop.Dispose();
    }
}
