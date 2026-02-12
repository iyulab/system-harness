namespace SystemHarness.Apps.Email;

/// <summary>
/// An email message for sending or receiving.
/// </summary>
public sealed class EmailMessage
{
    public string? MessageId { get; set; }
    public required string From { get; set; }
    public required IReadOnlyList<string> To { get; set; }
    public IReadOnlyList<string> Cc { get; set; } = [];
    public required string Subject { get; set; }
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public DateTimeOffset? Date { get; set; }
    public bool IsRead { get; set; }
    public IReadOnlyList<string> AttachmentNames { get; set; } = [];

    /// <summary>
    /// Attachments to include when sending. Ignored on received messages.
    /// </summary>
    public IReadOnlyList<EmailAttachment> Attachments { get; set; } = [];
}

/// <summary>
/// An email attachment with file name, data, and optional content type.
/// </summary>
public sealed record EmailAttachment(string FileName, byte[] Data, string? ContentType = null);
