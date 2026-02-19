namespace SystemHarness.Tests.Workflow;

[Trait("Category", "CI")]
public class ConvenienceHelpersUnitTests
{
    private static readonly Screenshot StubScreenshot = new()
    {
        Bytes = [0x89, 0x50, 0x4E, 0x47],
        MimeType = "image/png",
        Width = 800,
        Height = 600,
        Timestamp = DateTimeOffset.UtcNow,
    };

    private static OcrResult MakeOcrResult(params (string text, int x, int y, int w, int h)[] words)
    {
        var ocrWords = words.Select(w => new OcrWord
        {
            Text = w.text,
            BoundingRect = new Rectangle(w.x, w.y, w.w, w.h),
        }).ToArray();

        return new OcrResult
        {
            Text = string.Join(" ", words.Select(w => w.text)),
            Lines =
            [
                new OcrLine
                {
                    Text = string.Join(" ", words.Select(w => w.text)),
                    Words = ocrWords,
                    BoundingRect = new Rectangle(0, 0, 800, 600),
                }
            ],
        };
    }

    [Fact]
    public async Task CaptureAndRecognizeAsync_ReturnsBothResults()
    {
        var harness = new ConvStubHarness(StubScreenshot, MakeOcrResult(("Hello", 10, 10, 50, 20)));
        var (screenshot, ocr) = await ConvenienceHelpers.CaptureAndRecognizeAsync(harness);

        Assert.NotNull(screenshot);
        Assert.NotNull(ocr);
        Assert.Contains("Hello", ocr.Text);
    }

    [Fact]
    public async Task FindTextOnScreenAsync_FindsMatchingWord()
    {
        var harness = new ConvStubHarness(StubScreenshot, MakeOcrResult(
            ("Save", 100, 200, 40, 20),
            ("Cancel", 200, 200, 60, 20)));

        var words = await ConvenienceHelpers.FindTextOnScreenAsync(harness, "Save");

        Assert.Single(words);
        Assert.Equal("Save", words[0].Text);
    }

    [Fact]
    public async Task FindTextOnScreenAsync_CaseInsensitive()
    {
        var harness = new ConvStubHarness(StubScreenshot, MakeOcrResult(("HELLO", 10, 10, 50, 20)));
        var words = await ConvenienceHelpers.FindTextOnScreenAsync(harness, "hello");

        Assert.Single(words);
    }

    [Fact]
    public async Task FindTextOnScreenAsync_NoMatch_ReturnsEmpty()
    {
        var harness = new ConvStubHarness(StubScreenshot, MakeOcrResult(("Save", 100, 200, 40, 20)));
        var words = await ConvenienceHelpers.FindTextOnScreenAsync(harness, "Delete");

        Assert.Empty(words);
    }

    [Fact]
    public async Task ClickTextOnScreenAsync_TextNotFound_ThrowsHarnessException()
    {
        var harness = new ConvStubHarness(StubScreenshot, MakeOcrResult(("OK", 100, 100, 30, 20)));

        var ex = await Assert.ThrowsAsync<HarnessException>(
            () => ConvenienceHelpers.ClickTextOnScreenAsync(harness, "Missing"));

        Assert.Contains("Text not found", ex.Message);
    }

    [Fact]
    public async Task ClickTextOnScreenAsync_ClicksWordCenter()
    {
        var harness = new ConvStubHarness(StubScreenshot, MakeOcrResult(("OK", 100, 200, 40, 20)));
        await ConvenienceHelpers.ClickTextOnScreenAsync(harness, "OK");

        // Word center: x=100+40/2=120, y=200+20/2=210
        Assert.Single(harness.Mouse.Clicks);
        Assert.Equal(120, harness.Mouse.Clicks[0].X);
        Assert.Equal(210, harness.Mouse.Clicks[0].Y);
    }

    [Fact]
    public async Task ClickTextInWindowAsync_TextNotFound_IncludesWindowInMessage()
    {
        var harness = new ConvStubHarness(StubScreenshot, MakeOcrResult(("OK", 10, 10, 30, 20)));

        var ex = await Assert.ThrowsAsync<HarnessException>(
            () => ConvenienceHelpers.ClickTextInWindowAsync(harness, "Notepad", "Missing"));

        Assert.Contains("Notepad", ex.Message);
    }

    [Fact]
    public async Task FindTextOnScreenAsync_MultipleMatches_ReturnsAll()
    {
        var harness = new ConvStubHarness(StubScreenshot, MakeOcrResult(
            ("test", 10, 10, 30, 20),
            ("hello", 50, 10, 40, 20),
            ("test", 100, 10, 30, 20)));

        var words = await ConvenienceHelpers.FindTextOnScreenAsync(harness, "test");
        Assert.Equal(2, words.Count);
    }

    // --- Stubs ---

    private sealed class ConvStubHarness : IHarness
    {
        public ConvStubMouse Mouse { get; } = new();

        public ConvStubHarness(Screenshot screenshot, OcrResult ocrResult)
        {
            Screen = new ConvStubScreen(screenshot);
            Ocr = new ConvStubOcr(ocrResult);
        }

        public IShell Shell => null!;
        public IProcessManager Process => null!;
        public IFileSystem FileSystem => null!;
        public IWindow Window => null!;
        public IClipboard Clipboard => null!;
        public IScreen Screen { get; }
        IMouse IHarness.Mouse => Mouse;
        public IKeyboard Keyboard => null!;
        public IDisplay Display => null!;
        public ISystemInfo SystemInfo => null!;
        public IVirtualDesktop VirtualDesktop => null!;
        public IDialogHandler DialogHandler => null!;
        public IUIAutomation UIAutomation => null!;
        public IOcr Ocr { get; }
        public ITemplateMatcher TemplateMatcher => null!;
        public void Dispose() { }
    }

    internal sealed class ConvStubScreen : IScreen
    {
        private readonly Screenshot _ss;
        public ConvStubScreen(Screenshot ss) => _ss = ss;
        public Task<Screenshot> CaptureAsync(CaptureOptions? o = null, CancellationToken ct = default) => Task.FromResult(_ss);
        public Task<Screenshot> CaptureRegionAsync(int x, int y, int w, int h, CancellationToken ct = default) => Task.FromResult(_ss);
        public Task<Screenshot> CaptureWindowAsync(string t, CancellationToken ct = default) => Task.FromResult(_ss);
    }

    internal sealed class ConvStubOcr : IOcr
    {
        private readonly OcrResult _result;
        public ConvStubOcr(OcrResult result) => _result = result;
        public Task<OcrResult> RecognizeScreenAsync(OcrOptions? o = null, CancellationToken ct = default) => Task.FromResult(_result);
        public Task<OcrResult> RecognizeRegionAsync(int x, int y, int w, int h, OcrOptions? o = null, CancellationToken ct = default) => Task.FromResult(_result);
        public Task<OcrResult> RecognizeImageAsync(byte[] b, OcrOptions? o = null, CancellationToken ct = default) => Task.FromResult(_result);
    }

    internal sealed class ConvStubMouse : IMouse
    {
        public sealed record ClickRecord(int X, int Y, MouseButton Button);
        public List<ClickRecord> Clicks { get; } = [];

        public Task ClickAsync(int x, int y, MouseButton button = MouseButton.Left, CancellationToken ct = default)
        {
            Clicks.Add(new(x, y, button));
            return Task.CompletedTask;
        }

        public Task DoubleClickAsync(int x, int y, CancellationToken ct = default) => Task.CompletedTask;
        public Task RightClickAsync(int x, int y, CancellationToken ct = default) => Task.CompletedTask;
        public Task DragAsync(int fx, int fy, int tx, int ty, CancellationToken ct = default) => Task.CompletedTask;
        public Task ScrollAsync(int x, int y, int delta, CancellationToken ct = default) => Task.CompletedTask;
        public Task MoveAsync(int x, int y, CancellationToken ct = default) => Task.CompletedTask;
        public Task<(int X, int Y)> GetPositionAsync(CancellationToken ct = default) => Task.FromResult((0, 0));
    }
}
