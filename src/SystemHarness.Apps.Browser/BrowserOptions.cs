namespace SystemHarness.Apps.Browser;

/// <summary>
/// Configuration for launching a browser instance.
/// </summary>
public sealed class BrowserOptions
{
    /// <summary>
    /// Browser engine to use. Default is Chromium.
    /// </summary>
    public BrowserType BrowserType { get; set; } = BrowserType.Chromium;

    /// <summary>
    /// Run in headless mode (no visible window). Default is true.
    /// </summary>
    public bool Headless { get; set; } = true;

    /// <summary>
    /// Viewport width in pixels. Default is 1280.
    /// </summary>
    public int ViewportWidth { get; set; } = 1280;

    /// <summary>
    /// Viewport height in pixels. Default is 720.
    /// </summary>
    public int ViewportHeight { get; set; } = 720;

    /// <summary>
    /// Custom user agent string. Null uses the browser default.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Proxy server URL. Null means no proxy.
    /// </summary>
    public string? Proxy { get; set; }
}

/// <summary>
/// Browser engine type.
/// </summary>
public enum BrowserType
{
    Chromium,
    Firefox,
    WebKit
}
