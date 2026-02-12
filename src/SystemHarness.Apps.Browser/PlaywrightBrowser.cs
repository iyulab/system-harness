using Microsoft.Playwright;

namespace SystemHarness.Apps.Browser;

/// <summary>
/// Playwright-based implementation of <see cref="IBrowser"/>.
/// </summary>
public sealed class PlaywrightBrowser : IBrowser
{
    private IPlaywright? _playwright;
    private Microsoft.Playwright.IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _currentPage;

    public async Task LaunchAsync(BrowserOptions? options = null, CancellationToken ct = default)
    {
        options ??= new BrowserOptions();

        _playwright = await Playwright.CreateAsync();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = options.Headless,
        };
        if (options.Proxy is not null)
        {
            launchOptions.Proxy = new Proxy { Server = options.Proxy };
        }

        var browserType = options.BrowserType switch
        {
            Apps.Browser.BrowserType.Firefox => _playwright.Firefox,
            Apps.Browser.BrowserType.WebKit => _playwright.Webkit,
            _ => _playwright.Chromium,
        };

        _browser = await browserType.LaunchAsync(launchOptions);

        var contextOptions = new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = options.ViewportWidth,
                Height = options.ViewportHeight,
            },
        };
        if (options.UserAgent is not null)
            contextOptions.UserAgent = options.UserAgent;

        _context = await _browser.NewContextAsync(contextOptions);
        _currentPage = await _context.NewPageAsync();
    }

    public async Task NavigateAsync(string url, CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        await _currentPage!.GotoAsync(url);
    }

    public async Task<string> GetContentAsync(CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        return await _currentPage!.ContentAsync();
    }

    public Task<string> GetUrlAsync(CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_currentPage!.Url);
    }

    public async Task<string> GetTitleAsync(CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        return await _currentPage!.TitleAsync();
    }

    public async Task ClickAsync(string selector, CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        await _currentPage!.ClickAsync(selector);
    }

    public async Task FillAsync(string selector, string value, CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        await _currentPage!.FillAsync(selector, value);
    }

    public async Task SelectAsync(string selector, string value, CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        await _currentPage!.SelectOptionAsync(selector, value);
    }

    public async Task<string> GetTextAsync(string selector, CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        return await _currentPage!.InnerTextAsync(selector);
    }

    public async Task<string?> GetAttributeAsync(string selector, string attribute, CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        return await _currentPage!.GetAttributeAsync(selector, attribute);
    }

    public Task<IReadOnlyList<string>> GetTabsAsync(CancellationToken ct = default)
    {
        EnsureContext();
        ct.ThrowIfCancellationRequested();
        var pages = _context!.Pages;
        return Task.FromResult<IReadOnlyList<string>>(
            pages.Select(p => p.Url).ToList());
    }

    public async Task NewTabAsync(string? url = null, CancellationToken ct = default)
    {
        EnsureContext();
        ct.ThrowIfCancellationRequested();
        _currentPage = await _context!.NewPageAsync();
        if (url is not null)
            await _currentPage.GotoAsync(url);
    }

    public async Task CloseTabAsync(int index, CancellationToken ct = default)
    {
        EnsureContext();
        ct.ThrowIfCancellationRequested();
        var pages = _context!.Pages;
        if (index >= 0 && index < pages.Count)
        {
            await pages[index].CloseAsync();
            _currentPage = _context.Pages.LastOrDefault();
        }
    }

    public async Task GoBackAsync(CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        await _currentPage!.GoBackAsync();
    }

    public async Task GoForwardAsync(CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        await _currentPage!.GoForwardAsync();
    }

    public async Task<byte[]> ScreenshotAsync(CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        return await _currentPage!.ScreenshotAsync();
    }

    public async Task<byte[]> PdfAsync(CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        return await _currentPage!.PdfAsync();
    }

    public async Task<string> EvaluateAsync(string expression, CancellationToken ct = default)
    {
        EnsurePage();
        ct.ThrowIfCancellationRequested();
        var result = await _currentPage!.EvaluateAsync(expression);
        return result?.ToString() ?? string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
            await _context.CloseAsync();
        if (_browser is not null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private void EnsurePage()
    {
        if (_currentPage is null)
            throw new InvalidOperationException("Browser not launched. Call LaunchAsync first.");
    }

    private void EnsureContext()
    {
        if (_context is null)
            throw new InvalidOperationException("Browser not launched. Call LaunchAsync first.");
    }
}
