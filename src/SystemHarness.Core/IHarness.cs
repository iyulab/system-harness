namespace SystemHarness;

/// <summary>
/// Entry point for all computer-use primitives.
/// Provides access to Shell, Process, FileSystem, Window, Clipboard, Screen, Mouse, Keyboard, and Display.
/// </summary>
public interface IHarness : IDisposable
{
    /// <summary>
    /// Shell command execution (cmd, powershell, bash).
    /// </summary>
    IShell Shell { get; }

    /// <summary>
    /// Process management (start, kill, list, search).
    /// </summary>
    IProcessManager Process { get; }

    /// <summary>
    /// File and directory operations.
    /// </summary>
    IFileSystem FileSystem { get; }

    /// <summary>
    /// Window management (focus, resize, move, close, state control).
    /// </summary>
    IWindow Window { get; }

    /// <summary>
    /// System clipboard access (text, image, HTML, file drop).
    /// </summary>
    IClipboard Clipboard { get; }

    /// <summary>
    /// Screen capture (screenshots).
    /// </summary>
    IScreen Screen { get; }

    /// <summary>
    /// Mouse input simulation.
    /// </summary>
    IMouse Mouse { get; }

    /// <summary>
    /// Keyboard input simulation.
    /// </summary>
    IKeyboard Keyboard { get; }

    /// <summary>
    /// Multi-display management (monitor enumeration, DPI, bounds).
    /// </summary>
    IDisplay Display { get; }

    /// <summary>
    /// System information (environment variables, machine name, OS version).
    /// </summary>
    ISystemInfo SystemInfo { get; }

    /// <summary>
    /// Virtual desktop management (switch, enumerate).
    /// </summary>
    IVirtualDesktop VirtualDesktop { get; }

    /// <summary>
    /// System dialog handling (message boxes, file dialogs).
    /// </summary>
    IDialogHandler DialogHandler { get; }

    /// <summary>
    /// UI Automation — accessibility tree navigation and element manipulation.
    /// </summary>
    IUIAutomation UIAutomation { get; }

    /// <summary>
    /// OCR — optical character recognition from screen captures and images.
    /// </summary>
    IOcr Ocr { get; }

    /// <summary>
    /// Template matching — find template images within screenshots.
    /// </summary>
    ITemplateMatcher TemplateMatcher { get; }
}
