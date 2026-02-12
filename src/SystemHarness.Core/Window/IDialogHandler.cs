namespace SystemHarness;

/// <summary>
/// Handles system dialogs (message boxes, file dialogs, etc.).
/// </summary>
public interface IDialogHandler
{
    /// <summary>
    /// Checks whether a dialog window is currently open for the specified parent window.
    /// </summary>
    Task<bool> IsDialogOpenAsync(string? parentTitleOrHandle = null, CancellationToken ct = default);

    /// <summary>
    /// Types a file path into an open File Save/Open dialog.
    /// </summary>
    Task SetFileDialogPathAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Clicks a button in a dialog by its text label (e.g., "OK", "Cancel", "Save").
    /// </summary>
    Task ClickDialogButtonAsync(string buttonText, CancellationToken ct = default);

    /// <summary>
    /// Dismisses a message box by clicking the specified button or pressing Escape.
    /// </summary>
    Task DismissMessageBoxAsync(string? buttonText = null, CancellationToken ct = default);
}
