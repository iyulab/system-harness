namespace SystemHarness.Apps.Office;

/// <summary>
/// Read and write Office documents (Word, Excel, PowerPoint) using OpenXML.
/// No Office installation required — operates on .docx/.xlsx/.pptx files directly.
/// </summary>
public interface IDocumentReader
{
    Task<DocumentContent> ReadWordAsync(string filePath, CancellationToken ct = default);
    Task WriteWordAsync(string filePath, DocumentContent content, CancellationToken ct = default);

    /// <summary>
    /// Find and replace text in a Word document. Returns the number of replacements made.
    /// </summary>
    Task<int> FindReplaceWordAsync(string filePath, string find, string replace, CancellationToken ct = default);

    Task<SpreadsheetContent> ReadExcelAsync(string filePath, CancellationToken ct = default);
    Task WriteExcelAsync(string filePath, SpreadsheetContent content, CancellationToken ct = default);

    Task<PresentationContent> ReadPowerPointAsync(string filePath, CancellationToken ct = default);
    Task WritePowerPointAsync(string filePath, PresentationContent content, CancellationToken ct = default);
}
