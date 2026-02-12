namespace SystemHarness;

/// <summary>
/// Template matching — find a template image within a larger image.
/// Uses Normalized Cross-Correlation (NCC) for illumination-robust matching.
/// </summary>
public interface ITemplateMatcher
{
    /// <summary>
    /// Find all occurrences of a template within a screenshot.
    /// Returns matches sorted by confidence (highest first).
    /// </summary>
    /// <param name="screenshot">The image to search within.</param>
    /// <param name="templatePath">Path to the template image file (PNG/JPEG).</param>
    /// <param name="threshold">Minimum NCC score to consider a match (0.0 to 1.0, default 0.8).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<TemplateMatchResult>> FindAsync(
        Screenshot screenshot, string templatePath,
        double threshold = 0.8, CancellationToken ct = default);
}

/// <summary>
/// Result of a single template match, including location and confidence.
/// </summary>
public sealed record TemplateMatchResult
{
    /// <summary>X coordinate of the match (top-left corner) in the source image.</summary>
    public required int X { get; init; }

    /// <summary>Y coordinate of the match (top-left corner) in the source image.</summary>
    public required int Y { get; init; }

    /// <summary>Width of the matched region (same as template width).</summary>
    public required int Width { get; init; }

    /// <summary>Height of the matched region (same as template height).</summary>
    public required int Height { get; init; }

    /// <summary>Center X coordinate of the match.</summary>
    public int CenterX => X + Width / 2;

    /// <summary>Center Y coordinate of the match.</summary>
    public int CenterY => Y + Height / 2;

    /// <summary>NCC confidence score (0.0 to 1.0). Higher is better.</summary>
    public required double Confidence { get; init; }
}
