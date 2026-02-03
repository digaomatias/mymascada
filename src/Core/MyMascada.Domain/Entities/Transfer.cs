using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a transfer of funds between two accounts.
/// A transfer creates two linked transactions - a debit and a credit.
/// </summary>
public class Transfer : BaseEntity
{
    /// <summary>
    /// Unique identifier for this transfer
    /// </summary>
    public Guid TransferId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Amount being transferred (always positive)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code for the transfer amount
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "NZD";

    /// <summary>
    /// Exchange rate used if transferring between different currencies
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Fee charged for the transfer
    /// </summary>
    public decimal? FeeAmount { get; set; }

    /// <summary>
    /// Description or reason for the transfer
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Additional notes about the transfer
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Current status of the transfer
    /// </summary>
    public TransferStatus Status { get; set; } = TransferStatus.Pending;

    /// <summary>
    /// Date and time when the transfer was initiated
    /// </summary>
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time when the transfer was completed (if applicable)
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    // Foreign keys
    /// <summary>
    /// ID of the source account (money leaving)
    /// </summary>
    [Required]
    public int SourceAccountId { get; set; }

    /// <summary>
    /// ID of the destination account (money arriving)
    /// </summary>
    [Required]
    public int DestinationAccountId { get; set; }

    /// <summary>
    /// User who initiated the transfer
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    // Navigation properties
    /// <summary>
    /// Source account for the transfer
    /// </summary>
    public Account SourceAccount { get; set; } = null!;

    /// <summary>
    /// Destination account for the transfer
    /// </summary>
    public Account DestinationAccount { get; set; } = null!;

    /// <summary>
    /// User who owns this transfer
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Transactions created for this transfer (should be exactly 2)
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Checks if this transfer involves different currencies
    /// </summary>
    public bool IsMultiCurrency()
    {
        return SourceAccount?.Currency != DestinationAccount.Currency;
    }

    /// <summary>
    /// Gets the effective destination amount considering exchange rate
    /// </summary>
    public decimal GetDestinationAmount()
    {
        if (ExchangeRate.HasValue && IsMultiCurrency())
        {
            return Amount * ExchangeRate.Value;
        }
        return Amount;
    }

    /// <summary>
    /// Checks if the transfer is completed
    /// </summary>
    public bool IsCompleted()
    {
        return Status == TransferStatus.Completed;
    }

    /// <summary>
    /// Checks if the transfer can be cancelled
    /// </summary>
    public bool CanBeCancelled()
    {
        return Status == TransferStatus.Pending;
    }

    /// <summary>
    /// Marks the transfer as completed
    /// </summary>
    public void MarkAsCompleted()
    {
        Status = TransferStatus.Completed;
        CompletedDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the transfer as failed
    /// </summary>
    public void MarkAsFailed()
    {
        Status = TransferStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels the transfer if it's still pending
    /// </summary>
    public void Cancel()
    {
        if (CanBeCancelled())
        {
            Status = TransferStatus.Cancelled;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}