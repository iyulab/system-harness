namespace SystemHarness;

/// <summary>
/// Polling-based wait patterns for common UI automation scenarios.
/// Provides reliable "wait until" semantics for AI-driven workflows.
/// </summary>
public static class WaitHelpers
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Waits until the specified text appears on screen (via OCR).
    /// Useful for apps that lack accessibility tree support.
    /// </summary>
    /// <param name="harness">The harness instance.</param>
    /// <param name="text">The text to wait for (case-insensitive substring match).</param>
    /// <param name="timeout">Maximum time to wait. Default is 30 seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The OCR result that contains the matched text.</returns>
    /// <exception cref="TimeoutException">Thrown when the text doesn't appear within the timeout.</exception>
    public static async Task<OcrResult> WaitForTextAsync(
        IHarness harness, string text, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var result = await harness.Ocr.RecognizeScreenAsync(ct: ct);
            if (result.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
                return result;

            await Task.Delay(DefaultPollInterval, ct);
        }

        throw new TimeoutException($"Text \"{text}\" did not appear within {timeout ?? DefaultTimeout}.");
    }

    /// <summary>
    /// Waits until a UI element matching the condition appears in the window.
    /// </summary>
    /// <param name="harness">The harness instance.</param>
    /// <param name="titleOrHandle">Window title substring or handle string.</param>
    /// <param name="condition">The element search condition.</param>
    /// <param name="timeout">Maximum time to wait. Default is 30 seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first matching UI element.</returns>
    /// <exception cref="TimeoutException">Thrown when the element doesn't appear within the timeout.</exception>
    public static async Task<UIElement> WaitForElementAsync(
        IHarness harness, string titleOrHandle, UIElementCondition condition,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var element = await harness.UIAutomation.FindFirstAsync(titleOrHandle, condition, ct);
            if (element is not null)
                return element;

            await Task.Delay(DefaultPollInterval, ct);
        }

        throw new TimeoutException(
            $"Element matching condition did not appear in \"{titleOrHandle}\" within {timeout ?? DefaultTimeout}.");
    }

    /// <summary>
    /// Waits until a window reaches the specified state (Normal, Minimized, Maximized).
    /// </summary>
    /// <param name="harness">The harness instance.</param>
    /// <param name="titleOrHandle">Window title substring or handle string.</param>
    /// <param name="state">The desired window state.</param>
    /// <param name="timeout">Maximum time to wait. Default is 30 seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="TimeoutException">Thrown when the window doesn't reach the state within the timeout.</exception>
    public static async Task WaitForWindowStateAsync(
        IHarness harness, string titleOrHandle, WindowState state,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? DefaultTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var currentState = await harness.Window.GetStateAsync(titleOrHandle, ct);
            if (currentState == state)
                return;

            await Task.Delay(DefaultPollInterval, ct);
        }

        throw new TimeoutException(
            $"Window \"{titleOrHandle}\" did not reach state {state} within {timeout ?? DefaultTimeout}.");
    }
}
