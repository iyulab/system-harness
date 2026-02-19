using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace SystemHarness.Apps.Email;

/// <summary>
/// MailKit-based implementation of <see cref="IEmail"/>.
/// </summary>
public sealed class MailKitEmail : IEmail
{
    private ImapClient? _imap;
    private EmailConnectionOptions? _options;

    public async Task ConnectAsync(EmailConnectionOptions options, CancellationToken ct = default)
    {
        if (options.OAuth2Token is null && options.Password is null)
            throw new ArgumentException("Either Password or OAuth2Token must be provided.", nameof(options));

        _options = options;
        _imap = new ImapClient();

        var sslOptions = options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await _imap.ConnectAsync(options.ImapHost, options.ImapPort, sslOptions, ct);

        if (options.OAuth2Token is not null)
        {
            var oauth2 = new SaslMechanismOAuth2(options.Username, options.OAuth2Token);
            await _imap.AuthenticateAsync(oauth2, ct);
        }
        else
        {
            await _imap.AuthenticateAsync(options.Username, options.Password!, ct);
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_imap is { IsConnected: true })
        {
            await _imap.DisconnectAsync(true, ct);
        }
    }

    public async Task<IReadOnlyList<EmailMessage>> GetMessagesAsync(
        EmailQueryOptions? query = null, CancellationToken ct = default)
    {
        EnsureConnected();
        query ??= new EmailQueryOptions();

        var folder = await _imap!.GetFolderAsync(query.Folder, ct);
        await folder.OpenAsync(FolderAccess.ReadOnly, ct);

        var searchQuery = SearchQuery.All;
        if (query.UnreadOnly) searchQuery = searchQuery.And(SearchQuery.NotSeen);
        if (query.Since.HasValue) searchQuery = searchQuery.And(SearchQuery.SentSince(query.Since.Value.DateTime));
        if (query.SubjectFilter is not null) searchQuery = searchQuery.And(SearchQuery.SubjectContains(query.SubjectFilter));
        if (query.FromFilter is not null) searchQuery = searchQuery.And(SearchQuery.FromContains(query.FromFilter));

        var uids = await folder.SearchAsync(searchQuery, ct);
        var results = new List<EmailMessage>();

        foreach (var uid in uids.Take(query.MaxResults))
        {
            var message = await folder.GetMessageAsync(uid, ct);
            results.Add(MapMessage(message, uid.ToString()));
        }

        await folder.CloseAsync(false, ct);
        return results;
    }

    public async Task<EmailMessage> GetMessageAsync(string messageId, string? folder = null, CancellationToken ct = default)
    {
        EnsureConnected();

        var mailFolder = folder is null ? _imap!.Inbox : await _imap!.GetFolderAsync(folder, ct);
        await mailFolder.OpenAsync(FolderAccess.ReadOnly, ct);

        var uids = await mailFolder.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), ct);
        if (uids.Count == 0)
            throw new InvalidOperationException($"Message not found: {messageId}");

        var message = await mailFolder.GetMessageAsync(uids[0], ct);
        await mailFolder.CloseAsync(false, ct);
        return MapMessage(message, messageId);
    }

    public async Task<IReadOnlyList<string>> GetFoldersAsync(CancellationToken ct = default)
    {
        EnsureConnected();

        var personal = _imap!.GetFolder(_imap.PersonalNamespaces[0]);
        var folders = await personal.GetSubfoldersAsync(false, ct);
        return folders.Select(f => f.FullName).ToList();
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (_options is null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(message.From));
        foreach (var to in message.To) mime.To.Add(MailboxAddress.Parse(to));
        foreach (var cc in message.Cc) mime.Cc.Add(MailboxAddress.Parse(cc));
        mime.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder();
        if (message.TextBody is not null) bodyBuilder.TextBody = message.TextBody;
        if (message.HtmlBody is not null) bodyBuilder.HtmlBody = message.HtmlBody;

        foreach (var attachment in message.Attachments)
        {
            bodyBuilder.Attachments.Add(
                attachment.FileName,
                attachment.Data,
                ContentType.Parse(attachment.ContentType ?? "application/octet-stream"));
        }

        mime.Body = bodyBuilder.ToMessageBody();

        using var smtp = new SmtpClient();
        var smtpSslOptions = _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(_options.SmtpHost, _options.SmtpPort, smtpSslOptions, ct);

        if (_options.OAuth2Token is not null)
        {
            var oauth2 = new SaslMechanismOAuth2(_options.Username, _options.OAuth2Token);
            await smtp.AuthenticateAsync(oauth2, ct);
        }
        else
        {
            await smtp.AuthenticateAsync(_options.Username, _options.Password!, ct);
        }

        await smtp.SendAsync(mime, ct);
        await smtp.DisconnectAsync(true, ct);
    }

    public async Task MoveAsync(string messageId, string targetFolder, string? sourceFolder = null, CancellationToken ct = default)
    {
        EnsureConnected();

        var source = sourceFolder is null ? _imap!.Inbox : await _imap!.GetFolderAsync(sourceFolder, ct);
        await source.OpenAsync(FolderAccess.ReadWrite, ct);

        var uids = await source.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), ct);
        if (uids.Count > 0)
        {
            var target = await _imap!.GetFolderAsync(targetFolder, ct);
            await source.MoveToAsync(uids[0], target, ct);
        }

        await source.CloseAsync(false, ct);
    }

    public async Task DeleteAsync(string messageId, string? folder = null, CancellationToken ct = default)
    {
        EnsureConnected();

        var mailFolder = folder is null ? _imap!.Inbox : await _imap!.GetFolderAsync(folder, ct);
        await mailFolder.OpenAsync(FolderAccess.ReadWrite, ct);

        var uids = await mailFolder.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), ct);
        if (uids.Count > 0)
        {
            await mailFolder.AddFlagsAsync(uids[0], MessageFlags.Deleted, true, ct);
            await mailFolder.ExpungeAsync(ct);
        }

        await mailFolder.CloseAsync(false, ct);
    }

    public async Task MarkReadAsync(string messageId, bool read = true, string? folder = null, CancellationToken ct = default)
    {
        EnsureConnected();

        var mailFolder = folder is null ? _imap!.Inbox : await _imap!.GetFolderAsync(folder, ct);
        await mailFolder.OpenAsync(FolderAccess.ReadWrite, ct);

        var uids = await mailFolder.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), ct);
        if (uids.Count > 0)
        {
            if (read)
                await mailFolder.AddFlagsAsync(uids[0], MessageFlags.Seen, true, ct);
            else
                await mailFolder.RemoveFlagsAsync(uids[0], MessageFlags.Seen, true, ct);
        }

        await mailFolder.CloseAsync(false, ct);
    }

    public async Task<byte[]> DownloadAttachmentAsync(
        string messageId, string attachmentName, string? folder = null, CancellationToken ct = default)
    {
        EnsureConnected();

        var mailFolder = folder is null ? _imap!.Inbox : await _imap!.GetFolderAsync(folder, ct);
        await mailFolder.OpenAsync(FolderAccess.ReadOnly, ct);

        var uids = await mailFolder.SearchAsync(SearchQuery.HeaderContains("Message-Id", messageId), ct);
        if (uids.Count == 0)
            throw new InvalidOperationException($"Message not found: {messageId}");

        var message = await mailFolder.GetMessageAsync(uids[0], ct);
        await mailFolder.CloseAsync(false, ct);

        var attachment = message.Attachments
            .OfType<MimePart>()
            .FirstOrDefault(a => string.Equals(a.FileName, attachmentName, StringComparison.OrdinalIgnoreCase));

        if (attachment is null)
            throw new InvalidOperationException($"Attachment not found: {attachmentName}");

        using var ms = new MemoryStream();
        if (attachment.Content is not null)
            await attachment.Content.DecodeToAsync(ms, ct);
        return ms.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_imap is not null)
        {
            if (_imap.IsConnected)
                await _imap.DisconnectAsync(true);
            _imap.Dispose();
        }
    }

    private void EnsureConnected()
    {
        if (_imap is null || !_imap.IsConnected)
            throw new InvalidOperationException("Not connected. Call ConnectAsync first.");
    }

    private static EmailMessage MapMessage(MimeMessage message, string id)
    {
        return new EmailMessage
        {
            MessageId = message.MessageId ?? id,
            From = message.From.ToString(),
            To = message.To.Select(a => a.ToString()).ToList(),
            Cc = message.Cc.Select(a => a.ToString()).ToList(),
            Subject = message.Subject ?? string.Empty,
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
            Date = message.Date,
            AttachmentNames = message.Attachments
                .OfType<MimePart>()
                .Select(a => a.FileName ?? "unnamed")
                .ToList(),
        };
    }
}
