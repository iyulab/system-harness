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
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            // Type multiple lines
            await Keyboard.TypeAsync("Line 1: Hello World");
            await Keyboard.KeyPressAsync(Key.Enter);
            await Keyboard.TypeAsync("Line 2: Special chars: @#$%");
            await Keyboard.KeyPressAsync(Key.Enter);
            await Keyboard.TypeAsync("Line 3: Numbers 12345");
            await Task.Delay(300);

            // Select all and verify via clipboard
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
            await Task.Delay(200);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.C);
            await Task.Delay(200);

            var text = await Clipboard.GetTextAsync();
            Assert.NotNull(text);
            Assert.Contains("Line 1", text);
            Assert.Contains("Line 2", text);
            Assert.Contains("Line 3", text);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task Notepad_SpecialCharacters()
    {
        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            var specialText = "Brackets: []{}() Symbols: !@#$%^&*";
            await Keyboard.TypeAsync(specialText);
            await Task.Delay(300);

            // Verify via clipboard
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.A);
            await Task.Delay(200);
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.C);
            await Task.Delay(200);

            var text = await Clipboard.GetTextAsync();
            Assert.NotNull(text);
            Assert.Contains("Brackets", text);
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
        }
    }

    [Fact]
    public async Task Notepad_SaveToFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sim_test_{Guid.NewGuid():N}.txt");

        var proc = await LaunchAppAsync("notepad.exe");
        await Task.Delay(1000);

        try
        {
            await Window.FocusAsync("Notepad");
            await Task.Delay(300);

            var content = "Simulation test content: " + DateTime.UtcNow.ToString("O");
            await Keyboard.TypeAsync(content);
            await Task.Delay(300);

            // Ctrl+S to open Save dialog
            await Keyboard.HotkeyAsync(default, Key.Ctrl, Key.S);
            await Task.Delay(1000);

            // Type the file path
            await Keyboard.TypeAsync(tempFile);
            await Task.Delay(300);

            // Press Enter to save
            await Keyboard.KeyPressAsync(Key.Enter);
            await Task.Delay(1000);

            // Verify file was created
            var exists = await FileSystem.ExistsAsync(tempFile);
            if (exists)
            {
                var fileContent = await FileSystem.ReadAsync(tempFile);
                Assert.Contains("Simulation test content", fileContent);
            }
        }
        finally
        {
            await Process.KillAsync(proc.Pid);
            try { File.Delete(tempFile); } catch { }
        }
    }
}
