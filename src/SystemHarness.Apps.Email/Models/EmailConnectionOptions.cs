namespace SystemHarness.Apps.Email;

/// <summary>
/// Connection options for an email server.
/// </summary>
public sealed class EmailConnectionOptions
{
    /// <summary>
    /// IMAP/POP3 server host for receiving.
    /// </summary>
    public required string ImapHost { get; set; }

    /// <summary>
    /// IMAP/POP3 port (993 for IMAP SSL, 143 for IMAP).
    /// </summary>
    public int ImapPort { get; set; } = 993;

    /// <summary>
    /// SMTP server host for sending.
    /// </summary>
    public required string SmtpHost { get; set; }

    /// <summary>
    /// SMTP port (587 for STARTTLS, 465 for SSL).
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Username for authentication.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Password or app-specific password. Required unless OAuth2Token is provided.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// OAuth2 access token (alternative to password). If set, Password is ignored.
    /// </summary>
    public string? OAuth2Token { get; set; }

    /// <summary>
    /// Use SSL/TLS for connections. Default is true.
    /// </summary>
    public bool UseSsl { get; set; } = true;
}
