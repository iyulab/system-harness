using SystemHarness;

namespace SystemHarness.Apps.Office;

/// <summary>
/// Automate a running Office application via process management and UI automation.
/// Requires the Office app to be installed.
/// </summary>
public interface IOfficeApp
{
    /// <summary>
    /// Open a document in the corresponding Office app. Returns the process ID.
    /// </summary>
    Task<int> OpenDocumentAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Save the document in the specified window.
    /// </summary>
    Task SaveAsync(string titleOrHandle, CancellationToken ct = default);

    /// <summary>
    /// Close the document window, optionally saving first.
    /// </summary>
    Task CloseAsync(string titleOrHandle, bool save = true, CancellationToken ct = default);

    /// <summary>
    /// Get the UI automation tree for the document window.
    /// </summary>
    Task<UIElement> GetDocumentTreeAsync(string titleOrHandle, CancellationToken ct = default);
}
