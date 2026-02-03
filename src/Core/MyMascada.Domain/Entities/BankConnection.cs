using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a connection between an account and an external bank provider.
/// Stores provider-specific configuration and tracks synchronization state.
/// </summary>
public class BankConnection : BaseEntity
{
    /// <summary>
    /// ID of the account this connection is linked to
    /// </summary>
    [Required]
    public int AccountId { get; set; }

    /// <summary>
    /// User ID who owns this bank connection
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Identifier for the bank provider (e.g., "akahu", "email-forward")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted JSON containing provider-specific settings (OAuth tokens, API keys, etc.)
    /// </summary>
    public string? EncryptedSettings { get; set; }

    /// <summary>
    /// External account identifier from the provider (e.g., "acc_xxx")
    /// </summary>
    [MaxLength(100)]
    public string? ExternalAccountId { get; set; }

    /// <summary>
    /// Display name of the account from the provider
    /// </summary>
    [MaxLength(200)]
    public string? ExternalAccountName { get; set; }

    /// <summary>
    /// Whether this bank connection is currently active and enabled for syncing
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// UTC timestamp of the last successful synchronization
    /// </summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>
    /// Error message from the last failed synchronization attempt
    /// </summary>
    [MaxLength(1000)]
    public string? LastSyncError { get; set; }

    // Navigation properties

    /// <summary>
    /// Account that this bank connection is linked to
    /// </summary>
    public Account Account { get; set; } = null!;

    /// <summary>
    /// Collection of sync logs for this connection
    /// </summary>
    public ICollection<BankSyncLog> SyncLogs { get; set; } = new List<BankSyncLog>();
}
