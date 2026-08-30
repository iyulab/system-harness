namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests clipboard exchange between API and applications.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Integration")]
public class ClipboardExchangeTests : SimulationTestBase
{
    public ClipboardExchangeTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task TypeThenCopy_ApiReadsClipboard()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            var testText = $"Clipboard test {Guid.NewGuid():N}";
            await Keyboard.TypeAsync(testText, ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Select All + Copy
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.A });
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.C });
            await Task.Delay(200, TestContext.Current.CancellationToken);

            // Read clipboard via API
            var clipText = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(clipText);
            Assert.Contains(testText, clipText);
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            await Process.KillAsync(proc.Pid, CancellationToken.None);
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task ApiSetsClipboard_PasteIntoApp()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Set clipboard via API
            var testText = $"API clipboard {Guid.NewGuid():N}";
            await Clipboard.SetTextAsync(testText, TestContext.Current.CancellationToken);
            await Task.Delay(200, TestContext.Current.CancellationToken);

            // Paste into Notepad
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.V });
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Verify by selecting all and copying back
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.A });
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.C });
            await Task.Delay(200, TestContext.Current.CancellationToken);

            var result = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(result);
            Assert.Contains(testText, result);
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            await Process.KillAsync(proc.Pid, CancellationToken.None);
            #pragma warning restore xUnit1051
        }
    }

    [Fact]
    public async Task ClipboardFormats_AfterTextCopy()
    {
        await Clipboard.SetTextAsync("format test", TestContext.Current.CancellationToken);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        var formats = await Clipboard.GetAvailableFormatsAsync(TestContext.Current.CancellationToken);

        Assert.NotEmpty(formats);
        Assert.Contains(formats, f => f.Contains("UNICODETEXT") || f.Contains("TEXT"));
    }
}
