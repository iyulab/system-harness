namespace SystemHarness.Tests.Workflow;

[Trait("Category", "CI")]
public class WaitHelpersUnitTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);

    // --- WaitForTextAsync ---

    [Fact]
    public async Task WaitForTextAsync_TextFound_ReturnsOcrResult()
    {
        var harness = new WaitStubHarness(ocrText: "Hello World");
        var result = await WaitHelpers.WaitForTextAsync(harness, "Hello", ShortTimeout);
        Assert.Contains("Hello", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WaitForTextAsync_CaseInsensitive()
    {
        var harness = new WaitStubHarness(ocrText: "Hello World");
        var result = await WaitHelpers.WaitForTextAsync(harness, "hello", ShortTimeout);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task WaitForTextAsync_TextNotFound_ThrowsTimeout()
    {
        var harness = new WaitStubHarness(ocrText: "something else");
        await Assert.ThrowsAsync<TimeoutException>(
            () => WaitHelpers.WaitForTextAsync(harness, "missing", ShortTimeout));
    }

    [Fact]
    public async Task WaitForTextAsync_Cancellation_ThrowsOCE()
    {
        var harness = new WaitStubHarness(ocrText: "something");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => WaitHelpers.WaitForTextAsync(harness, "missing", ShortTimeout, cts.Token));
    }

    // --- WaitForElementAsync ---

    [Fact]
    public async Task WaitForElementAsync_ElementFound_ReturnsElement()
    {
        var element = new UIElement { Name = "OKButton", ControlType = UIControlType.Button };
        var harness = new WaitStubHarness(foundElement: element);
        var condition = new UIElementCondition { Name = "OKButton" };

        var result = await WaitHelpers.WaitForElementAsync(harness, "TestWindow", condition, ShortTimeout);

        Assert.NotNull(result);
        Assert.Equal("OKButton", result.Name);
    }

    [Fact]
    public async Task WaitForElementAsync_ElementNotFound_ThrowsTimeout()
    {
        var harness = new WaitStubHarness(foundElement: null);
        var condition = new UIElementCondition { Name = "Missing" };

        await Assert.ThrowsAsync<TimeoutException>(
            () => WaitHelpers.WaitForElementAsync(harness, "TestWindow", condition, ShortTimeout));
    }

    [Fact]
    public async Task WaitForElementAsync_Cancellation_ThrowsOCE()
    {
        var harness = new WaitStubHarness(foundElement: null);
        var condition = new UIElementCondition { Name = "Missing" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => WaitHelpers.WaitForElementAsync(harness, "TestWindow", condition, ShortTimeout, cts.Token));
    }

    // --- WaitForWindowStateAsync ---

    [Fact]
    public async Task WaitForWindowStateAsync_AlreadyInState_ReturnsImmediately()
    {
        var harness = new WaitStubHarness(windowState: WindowState.Maximized);

        await WaitHelpers.WaitForWindowStateAsync(harness, "TestWindow", WindowState.Maximized, ShortTimeout);
        // No exception = success
    }

    [Fact]
    public async Task WaitForWindowStateAsync_WrongState_ThrowsTimeout()
    {
        var harness = new WaitStubHarness(windowState: WindowState.Normal);

        await Assert.ThrowsAsync<TimeoutException>(
            () => WaitHelpers.WaitForWindowStateAsync(harness, "TestWindow", WindowState.Minimized, ShortTimeout));
    }

    [Fact]
    public async Task WaitForWindowStateAsync_Cancellation_ThrowsOCE()
    {
        var harness = new WaitStubHarness(windowState: WindowState.Normal);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => WaitHelpers.WaitForWindowStateAsync(harness, "TestWindow", WindowState.Maximized, ShortTimeout, cts.Token));
    }

    // --- Stubs ---

    private sealed class WaitStubHarness : IHarness
    {
        private readonly string _ocrText;
        private readonly UIElement? _foundElement;
        private readonly WindowState _windowState;

        public WaitStubHarness(
            string ocrText = "",
            UIElement? foundElement = null,
            WindowState windowState = WindowState.Normal)
        {
            _ocrText = ocrText;
            _foundElement = foundElement;
            _windowState = windowState;
            Ocr = new WaitStubOcr(ocrText);
            UIAutomation = new WaitStubUIAutomation(foundElement);
            Window = new WaitStubWindow(windowState);
        }

        public IShell Shell => null!;
        public IProcessManager Process => null!;
        public IFileSystem FileSystem => null!;
        public IWindow Window { get; }
        public IClipboard Clipboard => null!;
        public IScreen Screen => null!;
        public IMouse Mouse => null!;
        public IKeyboard Keyboard => null!;
        public IDisplay Display => null!;
        public ISystemInfo SystemInfo => null!;
        public IVirtualDesktop VirtualDesktop => null!;
        public IDialogHandler DialogHandler => null!;
        public IUIAutomation UIAutomation { get; }
        public IOcr Ocr { get; }
        public ITemplateMatcher TemplateMatcher => null!;
        public void Dispose() { }
    }

    private sealed class WaitStubOcr : IOcr
    {
        private readonly string _text;
        public WaitStubOcr(string text) => _text = text;

        public Task<OcrResult> RecognizeScreenAsync(OcrOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new OcrResult { Text = _text, Lines = [] });

        public Task<OcrResult> RecognizeRegionAsync(int x, int y, int width, int height, OcrOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new OcrResult { Text = _text, Lines = [] });

        public Task<OcrResult> RecognizeImageAsync(byte[] imageBytes, OcrOptions? options = null, CancellationToken ct = default) =>
            Task.FromResult(new OcrResult { Text = _text, Lines = [] });
    }

    private sealed class WaitStubUIAutomation : IUIAutomation
    {
        private readonly UIElement? _foundElement;
        public WaitStubUIAutomation(UIElement? foundElement) => _foundElement = foundElement;

        public Task<UIElement> GetAccessibilityTreeAsync(string titleOrHandle, int maxDepth = 5, CancellationToken ct = default) =>
            Task.FromResult(new UIElement { Name = "Root" });
        public Task<UIElement> GetRootElementAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.FromResult(new UIElement { Name = "Root" });
        public Task<UIElement> GetFocusedElementAsync(CancellationToken ct = default) =>
            Task.FromResult(new UIElement { Name = "Focused" });
        public Task<IReadOnlyList<UIElement>> FindAllAsync(string titleOrHandle, UIElementCondition condition, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UIElement>>(_foundElement is not null ? [_foundElement] : []);
        public Task<UIElement?> FindFirstAsync(string titleOrHandle, UIElementCondition condition, CancellationToken ct = default) =>
            Task.FromResult(_foundElement);
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

    private sealed class WaitStubWindow : IWindow
    {
        private readonly WindowState _state;
        public WaitStubWindow(WindowState state) => _state = state;

        public Task<IReadOnlyList<WindowInfo>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WindowInfo>>([]);
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
        public Task<WindowState> GetStateAsync(string titleOrHandle, CancellationToken ct = default) =>
            Task.FromResult(_state);
    }
}
