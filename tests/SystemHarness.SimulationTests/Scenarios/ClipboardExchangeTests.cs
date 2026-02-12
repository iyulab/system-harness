namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests clipboard exchange between API and applications.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Local")]
public class ClipboardExchangeTests : SimulationTestBase
{
    public ClipboardExchangeTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task TypeThenCopy_ApiReadsClipboard()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            var testText = $"Clipboard test {Guid.NewGuid():N}";
            await Keyboard.TypeAsync(testText);
            await Task.Delay(300);

            // Select All + Copy
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
            await Task.Delay(200);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.C);
            await Task.Delay(200);

            // Read clipboard via API
            var clipText = await Clipboard.GetTextAsync();
            Assert.NotNull(clipText);
            Assert.Contains(testText, clipText);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task ApiSetsClipboard_PasteIntoApp()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            // Set clipboard via API
            var testText = $"API clipboard {Guid.NewGuid():N}";
            await Clipboard.SetTextAsync(testText);
            await Task.Delay(200);

            // Paste into Notepad
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.V);
            await Task.Delay(300);

            // Verify by selecting all and copying back
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
            await Task.Delay(200);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.C);
            await Task.Delay(200);

            var result = await Clipboard.GetTextAsync();
            Assert.NotNull(result);
            Assert.Contains(testText, result);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task ClipboardFormats_AfterTextCopy()
    {
        await Clipboard.SetTextAsync("format test");
        await Task.Delay(200);

        var formats = await Clipboard.GetAvailableFormatsAsync();

        Assert.NotEmpty(formats);
        Assert.Contains(formats, f => f.Contains("UNICODETEXT") || f.Contains("TEXT"));
    }
}
