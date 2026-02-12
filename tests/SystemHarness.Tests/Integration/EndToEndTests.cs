using SystemHarness.Windows;

namespace SystemHarness.Tests.Integration;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class EndToEndTests : IAsyncLifetime
{
    private WindowsHarness _harness = null!;

    public Task InitializeAsync()
    {
        _harness = new WindowsHarness(new HarnessOptions
        {
            CommandPolicy = CommandPolicy.CreateDefault(),
            AuditLog = new InMemoryAuditLog(),
        });
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _harness.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ShellToFileSystem_WriteAndReadViaShell()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"e2e-{Guid.NewGuid()}.txt");
        try
        {
            // Write via shell
            await _harness.Shell.RunAsync("cmd", $"/C echo e2e-content > \"{tempFile}\"");

            // Read via filesystem
            var exists = await _harness.FileSystem.ExistsAsync(tempFile);
            Assert.True(exists);

            var content = await _harness.FileSystem.ReadAsync(tempFile);
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
        var proc = await _harness.Process.StartAsync("notepad.exe");
        await Task.Delay(500);

        try
        {
            // Verify it's running
            var isRunning = await _harness.Process.IsRunningAsync("notepad");
            Assert.True(isRunning);

            // List and find it
            var processes = await _harness.Process.ListAsync("notepad");
            Assert.NotEmpty(processes);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _harness.Process.KillAsync(proc.Pid); } catch { }
            await Task.Delay(500);

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
        using var screenshot = await _harness.Screen.CaptureAsync();

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
        });

        Assert.Equal("image/png", screenshot.MimeType);
        // PNG magic bytes: 0x89 P N G
        Assert.Equal(0x89, screenshot.Bytes[0]);
        Assert.Equal((byte)'P', screenshot.Bytes[1]);
    }

    [Fact]
    public async Task ClipboardRoundTrip()
    {
        var testText = $"e2e-clipboard-{Guid.NewGuid()}";

        await _harness.Clipboard.SetTextAsync(testText);
        var result = await _harness.Clipboard.GetTextAsync();

        Assert.Equal(testText, result);
    }

    [Fact]
    public async Task MousePosition_GetAndMove()
    {
        var (origX, origY) = await _harness.Mouse.GetPositionAsync();

        await _harness.Mouse.MoveAsync(100, 100);
        await Task.Delay(100);

        var (newX, newY) = await _harness.Mouse.GetPositionAsync();
        Assert.InRange(newX, 95, 105);
        Assert.InRange(newY, 95, 105);

        // Restore original position
        await _harness.Mouse.MoveAsync(origX, origY);
    }

    [Fact]
    public async Task FullFlow_LaunchTypeCapture()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();

        // Launch notepad
        var proc = await _harness.Process.StartAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            // Focus it
            await _harness.Window.FocusAsync("Notepad");
            await Task.Delay(300);

            // Type some text
            await _harness.Keyboard.TypeAsync("Hello from E2E test!");
            await Task.Delay(300);

            // Capture screen
            using var screenshot = await _harness.Screen.CaptureAsync();
            Assert.NotNull(screenshot);
            Assert.True(screenshot.Bytes.Length > 0);
        }
        finally
        {
            await NotepadHelper.CloseNotepadByPidAsync(proc.Pid);
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
            try { await _harness.Process.KillAsync(proc.Pid); } catch { }
            await Task.Delay(300);
        }
    }

    [Fact]
    public async Task PolicyBlocksDangerousViaFacade()
    {
        Assert.Throws<CommandPolicyException>(
            () => _harness.Shell.RunAsync("format", "C: /FS:NTFS").GetAwaiter().GetResult());

        // But safe commands work
        var result = await _harness.Shell.RunAsync("cmd", "/C echo safe");
        Assert.True(result.Success);
    }

    [Fact]
    public async Task WindowManagement_ListAndFocus()
    {
        var windows = await _harness.Window.ListAsync();
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
        var machine = await _harness.SystemInfo.GetMachineNameAsync();
        Assert.NotEmpty(machine);

        var user = await _harness.SystemInfo.GetUserNameAsync();
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
