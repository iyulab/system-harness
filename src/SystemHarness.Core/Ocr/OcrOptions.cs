namespace SystemHarness;

/// <summary>
/// Options for OCR recognition.
/// </summary>
public sealed class OcrOptions
{
    /// <summary>
    /// BCP-47 language tag for recognition (e.g., "en-US", "ko-KR"). Default is "en-US".
    /// </summary>
    public string Language { get; set; } = "en-US";

}
