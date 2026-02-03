using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents an email verification token for confirming user email addresses.
/// Only the SHA-256 hash of the token is stored, never the plaintext.
/// </summary>
public class EmailVerificationToken : BaseEntity<Guid>
{
    /// <summary>
    /// The user this token belongs to
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 hash of the actual token. The plaintext token is never stored.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// When this token expires. Default is 24 hours from creation.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether this token has been used
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// When the token was used (null if not used)
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// IP address that requested the verification email
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent of the client that requested the verification email
    /// </summary>
    public string? UserAgent { get; set; }

    // Navigation property
    public User User { get; set; } = null!;

    /// <summary>
    /// Whether this token is valid (not used and not expired)
    /// </summary>
    public bool IsValid => !IsUsed && ExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Whether this token has expired
    /// </summary>
    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;

    /// <summary>
    /// Marks this token as used
    /// </summary>
    public void MarkAsUsed()
    {
        IsUsed = true;
        UsedAt = DateTime.UtcNow;
    }
}
