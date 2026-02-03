namespace MyMascada.Application.Features.Email.DTOs;

/// <summary>
/// Represents an email message to be sent
/// </summary>
public record EmailMessage
{
    /// <summary>
    /// Recipient email address (required)
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Recipient display name (optional)
    /// </summary>
    public string? ToName { get; init; }

    /// <summary>
    /// Email subject line (required)
    /// </summary>
    public required string Subject { get; init; }

    /// <summary>
    /// Email body content (required)
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Whether the body is HTML content (default: true)
    /// </summary>
    public bool IsHtml { get; init; } = true;

    /// <summary>
    /// Override sender email address (uses default if null)
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// Override sender display name
    /// </summary>
    public string? FromName { get; init; }

    /// <summary>
    /// Reply-to email address (optional)
    /// </summary>
    public string? ReplyTo { get; init; }

    /// <summary>
    /// File attachments (optional)
    /// </summary>
    public IReadOnlyList<EmailAttachment>? Attachments { get; init; }

    /// <summary>
    /// Custom email headers (optional)
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Optional correlation ID for tracking across systems
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Represents a file attachment for an email
/// </summary>
public record EmailAttachment
{
    /// <summary>
    /// Filename including extension (e.g., "report.pdf")
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// File content as byte array
    /// </summary>
    public required byte[] Content { get; init; }

    /// <summary>
    /// MIME content type (default: application/octet-stream)
    /// </summary>
    public string ContentType { get; init; } = "application/octet-stream";
}

/// <summary>
/// Represents an email message that uses a template
/// </summary>
public record TemplatedEmailMessage
{
    /// <summary>
    /// Recipient email address (required)
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Recipient display name (optional)
    /// </summary>
    public string? ToName { get; init; }

    /// <summary>
    /// Template name (without extension) to render
    /// </summary>
    public required string TemplateName { get; init; }

    /// <summary>
    /// Data to bind to the template
    /// </summary>
    public required IReadOnlyDictionary<string, object> TemplateData { get; init; }

    /// <summary>
    /// Override sender email address (uses default if null)
    /// </summary>
    public string? From { get; init; }

    /// <summary>
    /// Reply-to email address (optional)
    /// </summary>
    public string? ReplyTo { get; init; }

    /// <summary>
    /// Optional correlation ID for tracking
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Locale for template localization (e.g., "en-US", "pt-BR").
    /// Falls back to default locale if template not found for specified locale.
    /// </summary>
    public string? Locale { get; init; }
}

/// <summary>
/// Result of an email send operation
/// </summary>
public record EmailResult
{
    /// <summary>
    /// Whether the email was sent successfully
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Message ID from the email provider (for tracking)
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Error message if sending failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Error code from the provider (if available)
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static EmailResult Succeeded(string? messageId = null)
        => new() { Success = true, MessageId = messageId };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static EmailResult Failed(string message, string? errorCode = null)
        => new() { Success = false, ErrorMessage = message, ErrorCode = errorCode };
}

/// <summary>
/// Information about an available email provider
/// </summary>
public record EmailProviderInfo
{
    /// <summary>
    /// Unique provider ID (e.g., "smtp", "postmark")
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Human-readable display name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Whether this is the currently configured default provider
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Whether this provider supports file attachments
    /// </summary>
    public bool SupportsAttachments { get; init; }
}
