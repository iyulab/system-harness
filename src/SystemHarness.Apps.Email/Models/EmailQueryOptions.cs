namespace SystemHarness.Apps.Email;

/// <summary>
/// Options for querying email messages.
/// </summary>
public sealed class EmailQueryOptions
{
    /// <summary>
    /// Mailbox folder to search. Default is "INBOX".
    /// </summary>
    public string Folder { get; set; } = "INBOX";

    /// <summary>
    /// Only return unread messages.
    /// </summary>
    public bool UnreadOnly { get; set; }

    /// <summary>
    /// Only return messages since this date.
    /// </summary>
    public DateTimeOffset? Since { get; set; }

    /// <summary>
    /// Filter by subject substring.
    /// </summary>
    public string? SubjectFilter { get; set; }

    /// <summary>
    /// Filter by sender substring.
    /// </summary>
    public string? FromFilter { get; set; }

    /// <summary>
    /// Maximum number of messages to return. Default is 50.
    /// </summary>
    public int MaxResults { get; set; } = 50;
}
