namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Root email configuration options
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Email";

    /// <summary>
    /// Active provider ID: "smtp" or "postmark"
    /// </summary>
    public string Provider { get; set; } = "smtp";

    /// <summary>
    /// Default sender email address
    /// </summary>
    public string DefaultFromEmail { get; set; } = string.Empty;

    /// <summary>
    /// Default sender display name
    /// </summary>
    public string DefaultFromName { get; set; } = "MyMascada";

    /// <summary>
    /// Enable email sending (false = log only, useful for development)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Template directory path (relative to app root)
    /// </summary>
    public string TemplateDirectory { get; set; } = "EmailTemplates";

    /// <summary>
    /// SMTP-specific settings
    /// </summary>
    public SmtpEmailOptions Smtp { get; set; } = new();

    /// <summary>
    /// Postmark-specific settings
    /// </summary>
    public PostmarkEmailOptions Postmark { get; set; } = new();
}

/// <summary>
/// SMTP provider configuration (for Postal, Mailtrap, etc.)
/// </summary>
public class SmtpEmailOptions
{
    /// <summary>
    /// SMTP server hostname
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port (587 for STARTTLS, 465 for SSL)
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// SMTP username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// SMTP password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Use STARTTLS (true) or direct SSL (false)
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>
    /// Use SSL/TLS. Set to false for unencrypted local connections (e.g., Postal on local network)
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Postmark provider configuration
/// </summary>
public class PostmarkEmailOptions
{
    /// <summary>
    /// Postmark server API token
    /// </summary>
    public string ServerToken { get; set; } = string.Empty;

    /// <summary>
    /// Message stream ID (e.g., "outbound" for transactional)
    /// </summary>
    public string MessageStream { get; set; } = "outbound";
}
