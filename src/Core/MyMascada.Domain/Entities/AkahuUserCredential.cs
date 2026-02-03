using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Stores Akahu API credentials per user.
/// Each user has their own Personal App credentials (App Token + User Token) that grant
/// access to all their connected bank accounts in Akahu.
/// </summary>
/// <remarks>
/// The User Token is per-user, NOT per-bank-connection. A single User Token grants
/// access to all banks the user has connected in their Akahu Personal App (e.g., ANZ + Amex).
/// BankConnection entities reference specific Akahu accounts (acc_xxx) but don't store tokens.
/// </remarks>
public class AkahuUserCredential : BaseEntity
{
    /// <summary>
    /// User ID who owns these Akahu credentials
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Encrypted Akahu App Token (app_token_xxx).
    /// Identifies the user's Personal App to Akahu.
    /// </summary>
    [Required]
    public string EncryptedAppToken { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted Akahu User Token (user_token_xxx).
    /// Grants access to all the user's connected bank accounts in Akahu.
    /// </summary>
    [Required]
    public string EncryptedUserToken { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Last time credentials were validated against Akahu API
    /// </summary>
    public DateTime? LastValidatedAt { get; set; }

    /// <summary>
    /// Optional: Error message from last validation failure
    /// </summary>
    [MaxLength(500)]
    public string? LastValidationError { get; set; }
}
