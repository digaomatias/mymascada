using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Records a single processed occurrence of a user-created recurring transaction.
/// One row exists per (recurring transaction, scheduled date) — this uniqueness
/// is what makes the daily job idempotent.
/// </summary>
public class RecurringTransactionOccurrence : BaseEntity
{
    /// <summary>
    /// Foreign key to the parent recurring transaction
    /// </summary>
    [Required]
    public int RecurringTransactionId { get; set; }

    /// <summary>
    /// The date this occurrence was scheduled for
    /// </summary>
    [Required]
    public DateTime ScheduledDate { get; set; }

    /// <summary>
    /// How the occurrence was handled (Notified, Created, Skipped)
    /// </summary>
    [Required]
    public RecurringTransactionOccurrenceStatus Status { get; set; }

    /// <summary>
    /// The auto-created transaction (only set when Status is Created)
    /// </summary>
    public int? TransactionId { get; set; }

    // Navigation properties

    /// <summary>
    /// Parent recurring transaction
    /// </summary>
    public RecurringTransaction RecurringTransaction { get; set; } = null!;

    /// <summary>
    /// Auto-created transaction (if any)
    /// </summary>
    public Transaction? Transaction { get; set; }
}
