using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Records the history and details of bank synchronization operations.
/// Tracks transaction counts, timing, and any errors that occurred.
/// </summary>
public class BankSyncLog : BaseEntity
{
    /// <summary>
    /// ID of the bank connection this sync log belongs to
    /// </summary>
    [Required]
    public int BankConnectionId { get; set; }

    /// <summary>
    /// Type of synchronization that was performed
    /// </summary>
    public BankSyncType SyncType { get; set; }

    /// <summary>
    /// Current status of the synchronization operation
    /// </summary>
    public BankSyncStatus Status { get; set; }

    /// <summary>
    /// UTC timestamp when the sync operation started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the sync operation completed (null if still in progress)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total number of transactions processed from the provider
    /// </summary>
    public int TransactionsProcessed { get; set; }

    /// <summary>
    /// Number of new transactions imported into the system
    /// </summary>
    public int TransactionsImported { get; set; }

    /// <summary>
    /// Number of transactions skipped (duplicates or already existing)
    /// </summary>
    public int TransactionsSkipped { get; set; }

    /// <summary>
    /// Error message if the sync failed
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// JSON containing additional sync metadata and details
    /// </summary>
    public string? Details { get; set; }

    // Navigation properties

    /// <summary>
    /// Bank connection that this sync log belongs to
    /// </summary>
    public BankConnection BankConnection { get; set; } = null!;
}
