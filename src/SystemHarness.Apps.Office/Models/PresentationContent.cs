namespace SystemHarness.Apps.Office;

/// <summary>
/// Content of a PowerPoint presentation.
/// </summary>
public sealed class PresentationContent
{
    /// <summary>
    /// Slides in order.
    /// </summary>
    public IReadOnlyList<PresentationSlide> Slides { get; init; } = [];
}

/// <summary>
/// A single slide in a presentation.
/// </summary>
public sealed class PresentationSlide
{
    /// <summary>
    /// Slide number (1-based).
    /// </summary>
    public int Number { get; init; }

    /// <summary>
    /// Text content extracted from all shapes on the slide (backward compatible).
    /// </summary>
    public IReadOnlyList<string> Texts { get; init; } = [];

    /// <summary>
    /// Shapes on the slide with position, type, and content.
    /// </summary>
    public IReadOnlyList<PresentationShape> Shapes { get; init; } = [];

    /// <summary>
    /// Images on the slide.
    /// </summary>
    public IReadOnlyList<PresentationImage> Images { get; init; } = [];

    /// <summary>
    /// Slide layout name (e.g., "Title Slide", "Blank").
    /// </summary>
    public string? LayoutName { get; init; }

    /// <summary>
    /// Notes text for the slide, if any.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// A shape on a slide.
/// </summary>
public sealed class PresentationShape
{
    /// <summary>
    /// Shape name (from NonVisualDrawingProperties).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Type of shape.
    /// </summary>
    public PresentationShapeType Type { get; init; }

    /// <summary>
    /// X position in EMU from top-left of the slide.
    /// </summary>
    public long? X { get; init; }

    /// <summary>
    /// Y position in EMU from top-left of the slide.
    /// </summary>
    public long? Y { get; init; }

    /// <summary>
    /// Width in EMU.
    /// </summary>
    public long? Width { get; init; }

    /// <summary>
    /// Height in EMU.
    /// </summary>
    public long? Height { get; init; }

    /// <summary>
    /// Text runs within this shape (if it contains text).
    /// </summary>
    public IReadOnlyList<DocumentRun> TextRuns { get; init; } = [];

    /// <summary>
    /// Plain text content of the shape (convenience).
    /// </summary>
    public string? Text { get; init; }
}

/// <summary>
/// An image on a slide.
/// </summary>
public sealed class PresentationImage
{
    /// <summary>
    /// Raw image bytes.
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// MIME content type (e.g., "image/png").
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Description/alt text.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Type of shape in a presentation.
/// </summary>
public enum PresentationShapeType
{
    /// <summary>
    /// A text box or auto-shape with text.
    /// </summary>
    TextBox,

    /// <summary>
    /// A picture/image shape.
    /// </summary>
    Picture,

    /// <summary>
    /// A table shape.
    /// </summary>
    Table,

    /// <summary>
    /// A chart shape.
    /// </summary>
    Chart,

    /// <summary>
    /// Other/unknown shape type.
    /// </summary>
    Other,
}
