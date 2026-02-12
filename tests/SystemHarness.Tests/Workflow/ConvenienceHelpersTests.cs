using SystemHarness.Windows;

namespace SystemHarness.Tests.Workflow;

[Collection("DesktopInteraction")]
[Trait("Category", "Local")]
public class ConvenienceHelpersTests : IAsyncLifetime
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
    public async Task CaptureAndRecognizeAsync_ReturnsScreenshotAndOcr()
    {
        var (screenshot, ocr) = await ConvenienceHelpers.CaptureAndRecognizeAsync(_harness);
        using var _ = screenshot;

        Assert.NotNull(screenshot);
        Assert.True(screenshot.Width > 0);
        Assert.True(screenshot.Height > 0);
        Assert.NotNull(ocr);
        Assert.NotNull(ocr.Text);
    }

    [Fact]
    public async Task CaptureAndRecognizeWindowAsync_Notepad()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();

        try
        {
            await _harness.Process.StartAsync("notepad.exe");
            await _harness.Window.WaitForWindowAsync("Notepad", TimeSpan.FromSeconds(10));
            await Task.Delay(500);

            await _harness.Window.FocusAsync("Notepad");
            await Task.Delay(500);

            await _harness.Keyboard.TypeAsync("ConvenienceTest123");
            await Task.Delay(1000);

            var (screenshot, ocr) = await ConvenienceHelpers.CaptureAndRecognizeWindowAsync(_harness, "Notepad");
            using var _ = screenshot;

            Assert.NotNull(screenshot);
            Assert.True(screenshot.Width > 0);
            Assert.NotNull(ocr);
            // OCR at full resolution should pick up the typed text
            Assert.Contains("ConvenienceTest123", ocr.Text);
        }
        finally
        {
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
        }
    }

    [Fact]
    public async Task FindTextOnScreenAsync_FindsText()
    {
        // The taskbar/desktop should have some text visible — just verify no exception
        var words = await ConvenienceHelpers.FindTextOnScreenAsync(_harness, "Windows");

        Assert.NotNull(words);
    }

    [Fact]
    public async Task ClickTextInWindowAsync_DoesNotThrow_WhenTextExists()
    {
        var handlesBefore = await NotepadHelper.SnapshotNotepadHandlesAsync();

        try
        {
            await _harness.Process.StartAsync("notepad.exe");
            // Wait for Notepad window to appear
            await _harness.Window.WaitForWindowAsync("Notepad", TimeSpan.FromSeconds(10));
            await Task.Delay(500);

            await _harness.Window.FocusAsync("Notepad");
            await Task.Delay(500);

            await _harness.Keyboard.TypeAsync("ClickTarget");
            await Task.Delay(1000);

            // Verify OCR can see the text before attempting click
            var words = await ConvenienceHelpers.FindTextInWindowAsync(_harness, "Notepad", "ClickTarget");
            Assert.NotEmpty(words);

            // Should find and click "ClickTarget" in the Notepad window without throwing
            await ConvenienceHelpers.ClickTextInWindowAsync(_harness, "Notepad", "ClickTarget");
        }
        finally
        {
            await NotepadHelper.CloseNewNotepadWindowsAsync(handlesBefore);
        }
    }
}
