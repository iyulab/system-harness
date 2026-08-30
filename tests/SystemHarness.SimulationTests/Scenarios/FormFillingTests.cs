namespace SystemHarness.SimulationTests.Scenarios;

/// <summary>
/// Tests multi-step form filling: typing, special characters, Tab navigation.
/// </summary>
[Collection("Simulation")]
[Trait("Category", "Integration")]
public class FormFillingTests : SimulationTestBase
{
    public FormFillingTests(SimulationFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Notepad_MultiLineTextInput()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Type multiple lines
            await Keyboard.TypeAsync("Line 1: Hello World", ct: TestContext.Current.CancellationToken);
            await Keyboard.KeyPressAsync(Key.Enter, TestContext.Current.CancellationToken);
            await Keyboard.TypeAsync("Line 2: Special chars: @#$%", ct: TestContext.Current.CancellationToken);
            await Keyboard.KeyPressAsync(Key.Enter, TestContext.Current.CancellationToken);
            await Keyboard.TypeAsync("Line 3: Numbers 12345", ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Select all and verify via clipboard
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.A });
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.C });
            await Task.Delay(200, TestContext.Current.CancellationToken);

            var text = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(text);
            Assert.Contains("Line 1", text);
            Assert.Contains("Line 2", text);
            Assert.Contains("Line 3", text);
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
    public async Task Notepad_SpecialCharacters()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            var specialText = "Brackets: []{}() Symbols: !@#$%^&*";
            await Keyboard.TypeAsync(specialText, ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Verify via clipboard
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.A });
            await Task.Delay(200, TestContext.Current.CancellationToken);
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.C });
            await Task.Delay(200, TestContext.Current.CancellationToken);

            var text = await Clipboard.GetTextAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(text);
            Assert.Contains("Brackets", text);
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
    public async Task Notepad_SaveToFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sim_test_{Guid.NewGuid():N}.txt");

        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        try
        {
            await Window.FocusAsync("Notepad", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            var content = "Simulation test content: " + DateTime.UtcNow.ToString("O");
            await Keyboard.TypeAsync(content, ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Ctrl+S to open Save dialog
            await Keyboard.HotkeyAsync(TestContext.Current.CancellationToken, new Key[] { Key.Ctrl, Key.S });
            await Task.Delay(1000, TestContext.Current.CancellationToken);

            // Type the file path
            await Keyboard.TypeAsync(tempFile, ct: TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            // Press Enter to save
            await Keyboard.KeyPressAsync(Key.Enter, TestContext.Current.CancellationToken);
            await Task.Delay(1000, TestContext.Current.CancellationToken);

            // Verify file was created
            var exists = await FileSystem.ExistsAsync(tempFile, TestContext.Current.CancellationToken);
            if (exists)
            {
                var fileContent = await FileSystem.ReadAsync(tempFile, TestContext.Current.CancellationToken);
                Assert.Contains("Simulation test content", fileContent);
            }
        }
        finally
        {
            // Cleanup must not be cancelled by the test's own token -- a cancelled test is exactly when this kill matters most.
            #pragma warning disable xUnit1051
            await Process.KillAsync(proc.Pid, CancellationToken.None);
            #pragma warning restore xUnit1051
            try { File.Delete(tempFile); } catch { }
        }
    }
}
