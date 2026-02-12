namespace SystemHarness.Apps.Office;

/// <summary>
/// Read and write HWP documents in HWPX (OWPML) format.
/// No Hangul (HWP) application required — operates on .hwpx files directly.
/// HWPX is a ZIP container with XML following the OWPML standard (KS X 6101).
/// </summary>
/// <remarks>
/// Legacy binary .hwp format is not supported by this reader.
/// For legacy .hwp support, use COM automation via IHwpApp (requires HWP installed).
/// </remarks>
public interface IHwpReader
{
    /// <summary>
    /// Read a HWPX document and extract its content.
    /// </summary>
    /// <param name="filePath">Path to the .hwpx file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed document content.</returns>
    Task<HwpContent> ReadHwpxAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Write content to a new HWPX document.
    /// </summary>
    /// <param name="filePath">Output path for the .hwpx file.</param>
    /// <param name="content">Document content to write.</param>
    /// <param name="ct">Cancellation token.</param>
    Task WriteHwpxAsync(string filePath, HwpContent content, CancellationToken ct = default);

    /// <summary>
    /// Find and replace text in a HWPX document. Returns the number of replacements made.
    /// </summary>
    /// <param name="filePath">Path to the .hwpx file (modified in-place).</param>
    /// <param name="find">Text to find.</param>
    /// <param name="replace">Replacement text.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<int> FindReplaceAsync(string filePath, string find, string replace, CancellationToken ct = default);
}
