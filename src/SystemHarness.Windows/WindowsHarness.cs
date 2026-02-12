namespace SystemHarness.Windows;

/// <summary>
/// Windows implementation of <see cref="IHarness"/>.
/// Aggregates all Windows computer-use primitives into a single entry point.
/// Optionally applies command policy and audit logging via <see cref="HarnessOptions"/>.
/// </summary>
public sealed class WindowsHarness : IHarness
{
    private readonly WindowsScreen _screen;
    private readonly WindowsUIAutomation _uiAutomation;
    private int _disposed;

    /// <summary>
    /// Creates a Windows harness with default settings (no policy, no auditing).
    /// </summary>
    public WindowsHarness() : this(null) { }

    /// <summary>
    /// Creates a Windows harness with the specified options.
    /// </summary>
    /// <param name="options">Configuration for safety features and defaults. Null uses defaults.</param>
    public WindowsHarness(HarnessOptions? options)
    {
        options ??= new HarnessOptions();

        IShell shell = new WindowsShell();

        // Apply command policy decorator
        if (options.CommandPolicy is not null)
            shell = new PolicyEnforcingShell(shell, options.CommandPolicy);

        // Apply audit logging decorator
        if (options.AuditLog is not null)
            shell = new AuditingShell(shell, options.AuditLog);

        Shell = shell;
        Process = new WindowsProcessManager();
        FileSystem = new WindowsFileSystem();
        Window = new WindowsWindow();
        Clipboard = new WindowsClipboard();
        _screen = new WindowsScreen();
        Screen = _screen;
        Mouse = new WindowsMouse();
        Keyboard = new WindowsKeyboard();
        Display = new WindowsDisplay();
        SystemInfo = new WindowsSystemInfo();
        VirtualDesktop = new WindowsVirtualDesktop();
        DialogHandler = new WindowsDialogHandler();
        _uiAutomation = new WindowsUIAutomation();
        UIAutomation = _uiAutomation;
        Ocr = new WindowsOcr(_screen);
        TemplateMatcher = new SkiaTemplateMatcher();
    }

    /// <inheritdoc />
    public IShell Shell { get; }

    /// <inheritdoc />
    public IProcessManager Process { get; }

    /// <inheritdoc />
    public IFileSystem FileSystem { get; }

    /// <inheritdoc />
    public IWindow Window { get; }

    /// <inheritdoc />
    public IClipboard Clipboard { get; }

    /// <inheritdoc />
    public IScreen Screen { get; }

    /// <inheritdoc />
    public IMouse Mouse { get; }

    /// <inheritdoc />
    public IKeyboard Keyboard { get; }

    /// <inheritdoc />
    public IDisplay Display { get; }

    /// <inheritdoc />
    public ISystemInfo SystemInfo { get; }

    /// <inheritdoc />
    public IVirtualDesktop VirtualDesktop { get; }

    /// <inheritdoc />
    public IDialogHandler DialogHandler { get; }

    /// <inheritdoc />
    public IUIAutomation UIAutomation { get; }

    /// <inheritdoc />
    public IOcr Ocr { get; }

    /// <inheritdoc />
    public ITemplateMatcher TemplateMatcher { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _screen.Dispose();
        _uiAutomation.Dispose();
        (Shell as IDisposable)?.Dispose();
        (Process as IDisposable)?.Dispose();
        (FileSystem as IDisposable)?.Dispose();
        (Window as IDisposable)?.Dispose();
        (Clipboard as IDisposable)?.Dispose();
        (Mouse as IDisposable)?.Dispose();
        (Keyboard as IDisposable)?.Dispose();
        (Display as IDisposable)?.Dispose();
        (SystemInfo as IDisposable)?.Dispose();
        (VirtualDesktop as IDisposable)?.Dispose();
        (DialogHandler as IDisposable)?.Dispose();
    }
}
