namespace SystemHarness.Apps.Email;

/// <summary>
/// Email automation — IMAP/SMTP send and receive with attachment support.
/// </summary>
public interface IEmail : IAsyncDisposable
{
    // Connection
    Task ConnectAsync(EmailConnectionOptions options, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    // Receive
    Task<IReadOnlyList<EmailMessage>> GetMessagesAsync(EmailQueryOptions? query = null, CancellationToken ct = default);
    Task<EmailMessage> GetMessageAsync(string messageId, string? folder = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetFoldersAsync(CancellationToken ct = default);

    // Send
    Task SendAsync(EmailMessage message, CancellationToken ct = default);

    // Manipulation
    Task MoveAsync(string messageId, string targetFolder, string? sourceFolder = null, CancellationToken ct = default);
    Task DeleteAsync(string messageId, string? folder = null, CancellationToken ct = default);
    Task MarkReadAsync(string messageId, bool read = true, string? folder = null, CancellationToken ct = default);

    // Attachments
    Task<byte[]> DownloadAttachmentAsync(string messageId, string attachmentName, string? folder = null, CancellationToken ct = default);
}
