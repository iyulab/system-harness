namespace SystemHarness.Tests.Workflow;

[Trait("Category", "CI")]
public class HarnessObserverTests
{
    private static readonly WindowInfo TestWindow = new()
    {
        Handle = 12345,
        Title = "Test Window",
        ProcessId = 100,
        Bounds = new Rectangle(0, 0, 800, 600),
    };

    private static readonly Screenshot TestScreenshot = new()
    {
        Bytes = [0xFF, 0xD8, 0xFF],
        MimeType = "image/jpeg",
        Width = 800,
        Height = 600,
        Timestamp = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task ObserveAsync_DefaultOptions_IncludesScreenshotAndTree()
    {
        var harness = new StubHarness();
        var observer = new HarnessObserver(harness);

        using var obs = await observer.ObserveAsync("Test Window");

        Assert.NotNull(obs.Screenshot);
        Assert.NotNull(obs.AccessibilityTree);
        Assert.Null(obs.OcrText); // OCR is off by default
        Assert.NotNull(obs.WindowInfo);
        Assert.Equal("Test Window", obs.WindowInfo.Title);
    }

    [Fact]
    public async Task ObserveAsync_ScreenshotOnly_NoTreeNoOcr()
    {
        var harness = new StubHarness();
        var observer = new HarnessObserver(harness);
        var options = new ObserveOptions
        {
            IncludeScreenshot = true,
            IncludeAccessibilityTree = false,
            IncludeOcr = false,
        };

        using var obs = await observer.ObserveAsync("Test Window", options);

        Assert.NotNull(obs.Screenshot);
        Assert.Null(obs.AccessibilityTree);
        Assert.Null(obs.OcrText);
    }

    [Fact]
    public async Task ObserveAsync_OcrEnabled_ReturnsOcrResult()
    {
        var harness = new StubHarness();
        var observer = new HarnessObserver(harness);
        var options = new ObserveOptions
        {
            IncludeScreenshot = false,
            IncludeAccessibilityTree = false,
            IncludeOcr = true,
        };

        using var obs = await observer.ObserveAsync("Test Window", options);

        Assert.Null(obs.Screenshot); // Screenshot was only for OCR
        Assert.NotNull(obs.OcrText);
    }

    [Fact]
    public async Task ObserveAsync_AllDisabled_ReturnsMinimalObservation()
    {
        var harness = new StubHarness();
        var observer = new HarnessObserver(harness);
        var options = new ObserveOptions
        {
            IncludeScreenshot = false,
            IncludeAccessibilityTree = false,
            IncludeOcr = false,
        };

        using var obs = await observer.ObserveAsync("Test Window", options);

        Assert.Null(obs.Screenshot);
        Assert.Null(obs.AccessibilityTree);
        Assert.Null(obs.OcrText);
        Assert.NotNull(obs.WindowInfo);
    }

    [Fact]
    public async Task ObserveAsync_ByHandle_FindsWindow()
    {
        var harness = new StubHarness();
        var observer = new HarnessObserver(harness);
        var options = new ObserveOptions
        {
            IncludeScreenshot = false,
            IncludeAccessibilityTree = false,
        };

        using var obs = await observer.ObserveAsync("12345", options);

        Assert.NotNull(obs.WindowInfo);
        Assert.Equal(12345, obs.WindowInfo.Handle);
    }

    [Fact]
    public void Constructor_NullHarness_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HarnessObserver(null!));
    }

    // --- Stub implementations ---

    private class StubHarness : IHarness
    {
        public IShell Shell => null!;
        public IProcessManager Process => null!;
        public IFileSystem FileSystem => null!;
        public IWindow Window { get; } = new StubWindow();
        public IClipboard Clipboard => null!;
        public IScreen Screen { get; } = new StubScreen();
        public IMouse Mouse => null!;
        public IKeyboard Keyboard => null!;
        public IDisplay Display => null!;
        public ISystemInfo SystemInfo => null!;
        public IVirtualDesktop VirtualDesktop => null!;
        public IDialogHandler DialogHandler => null!;
        public IUIAutomation UIAutomation { get; } = new StubUIAutomation();
        public IOcr Ocr { get; } = new StubOcr();
        public ITemplateMatcher TemplateMatcher => null!;
        public void Dispose() { }
    }

    private class StubWindow : IWindow
    {
        public Task<IReadOnlyList<WindowInfo>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WindowInfo>>([TestWindow]);
        public Task FocusAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task MinimizeAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task MaximizeAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task RestoreAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task CloseAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task MoveAsync(string titleOrHandle, int x, int y, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task ResizeAsync(string titleOrHandle, int width, int height, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private class StubScreen : IScreen
    {
        public Task<Screenshot> CaptureAsync(CaptureOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(TestScreenshot);
        public Task<Screenshot> CaptureRegionAsync(int x, int y, int width, int height, CancellationToken ct = default) =>
            Task.FromResult(TestScreenshot);
        public Task<Screenshot> CaptureRegionAsync(int x, int y, int width, int height, CaptureOptions? options, CancellationToken ct = default) =>
            Task.FromResult(TestScreenshot);
        public Task<Screenshot> CaptureWindowAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.FromResult(TestScreenshot);
        public Task<Screenshot> CaptureWindowAsync(string titleOrHandle, CaptureOptions? options, CancellationToken ct = default) =>
            Task.FromResult(TestScreenshot);
        public Task<Screenshot> CaptureWindowRegionAsync(string titleOrHandle, int relativeX, int relativeY, int width, int height, CaptureOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(TestScreenshot);
        public Task<Screenshot> CaptureMonitorAsync(int monitorIndex, CaptureOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(TestScreenshot);
    }

    private class StubUIAutomation : IUIAutomation
    {
        public Task<UIElement> GetAccessibilityTreeAsync(string titleOrHandle, int maxDepth = 5, CancellationToken ct = default) =>
            Task.FromResult(new UIElement
            {
                AutomationId = "root",
                ClassName = "Window",
                ControlType = UIControlType.Window,
                Name = "Test Window",
            });
        public Task<UIElement> GetRootElementAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.FromResult(new UIElement { Name = "Root" });
        public Task<UIElement> GetFocusedElementAsync(CancellationToken ct = default) =>
            Task.FromResult(new UIElement { Name = "Focused" });
        public Task<IReadOnlyList<UIElement>> FindAllAsync(string titleOrHandle, UIElementCondition condition, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UIElement>>([]);
        public Task<UIElement?> FindFirstAsync(string titleOrHandle, UIElementCondition condition, CancellationToken ct = default) =>
            Task.FromResult<UIElement?>(null);
        public Task ClickElementAsync(UIElement element, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task SetValueAsync(UIElement element, string value, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task InvokeAsync(UIElement element, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task SelectAsync(UIElement element, string itemText, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task ExpandAsync(UIElement element, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private class StubOcr : IOcr
    {
        public Task<OcrResult> RecognizeScreenAsync(OcrOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new OcrResult { Text = "test", Lines = [] });
        public Task<OcrResult> RecognizeRegionAsync(int x, int y, int width, int height, OcrOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new OcrResult { Text = "test", Lines = [] });
        public Task<OcrResult> RecognizeImageAsync(byte[] imageBytes, OcrOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new OcrResult { Text = "test", Lines = [] });
    }
}
