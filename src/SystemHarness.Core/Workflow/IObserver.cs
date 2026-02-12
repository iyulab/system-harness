namespace SystemHarness;

/// <summary>
/// Hybrid observation — combines accessibility tree, screenshot, and OCR
/// into a single call, providing AI agents with a complete view of window state.
/// </summary>
public interface IObserver
{
    /// <summary>
    /// Observes a window by combining screenshot, accessibility tree, and OCR results.
    /// This is the core pattern for AI-driven computer use: one call returns everything
    /// the agent needs to understand the current UI state.
    /// </summary>
    /// <param name="titleOrHandle">Window title substring or handle string.</param>
    /// <param name="options">Controls which observation channels to include.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Observation> ObserveAsync(string titleOrHandle, ObserveOptions? options = null, CancellationToken ct = default);
}
