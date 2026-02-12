namespace SystemHarness.Apps.Browser;

/// <summary>
/// Browser automation — navigate, interact with elements, take screenshots, and manage tabs.
/// Built on Playwright.
/// </summary>
public interface IBrowser : IAsyncDisposable
{
    Task LaunchAsync(BrowserOptions? options = null, CancellationToken ct = default);
    Task NavigateAsync(string url, CancellationToken ct = default);
    Task<string> GetContentAsync(CancellationToken ct = default);
    Task<string> GetUrlAsync(CancellationToken ct = default);
    Task<string> GetTitleAsync(CancellationToken ct = default);

    // Element interaction
    Task ClickAsync(string selector, CancellationToken ct = default);
    Task FillAsync(string selector, string value, CancellationToken ct = default);
    Task SelectAsync(string selector, string value, CancellationToken ct = default);
    Task<string> GetTextAsync(string selector, CancellationToken ct = default);
    Task<string?> GetAttributeAsync(string selector, string attribute, CancellationToken ct = default);

    // Tab/navigation
    Task<IReadOnlyList<string>> GetTabsAsync(CancellationToken ct = default);
    Task NewTabAsync(string? url = null, CancellationToken ct = default);
    Task CloseTabAsync(int index, CancellationToken ct = default);
    Task GoBackAsync(CancellationToken ct = default);
    Task GoForwardAsync(CancellationToken ct = default);

    // Screenshot/PDF
    Task<byte[]> ScreenshotAsync(CancellationToken ct = default);
    Task<byte[]> PdfAsync(CancellationToken ct = default);

    // JavaScript
    Task<string> EvaluateAsync(string expression, CancellationToken ct = default);
}
