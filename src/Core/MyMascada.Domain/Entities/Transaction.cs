using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a financial transaction within an account.
/// Core entity that tracks money movement and categorization.
/// </summary>
public class Transaction : BaseEntity
{
    /// <summary>
    /// Amount of the transaction (positive for income, negative for expenses)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Date when the transaction occurred
    /// </summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// Original description from bank/source
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly description that can be edited
    /// </summary>
    [MaxLength(500)]
    public string? UserDescription { get; set; }

    /// <summary>
    /// Current status of the transaction
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Cleared;

    /// <summary>
    /// Source of the transaction data
    /// </summary>
    public TransactionSource Source { get; set; } = TransactionSource.Manual;

    /// <summary>
    /// External transaction ID from bank or import source
    /// </summary>
    [MaxLength(100)]
    public string? ExternalId { get; set; }

    /// <summary>
    /// Reference number or check number
    /// </summary>
    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// Additional notes about the transaction
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Bank-provided category from import source (e.g., Akahu).
    /// Used by the categorization pipeline to map to user categories.
    /// </summary>
    [MaxLength(200)]
    public string? BankCategory { get; set; }

    /// <summary>
    /// Physical location where transaction occurred
    /// </summary>
    [MaxLength(200)]
    public string? Location { get; set; }

    /// <summary>
    /// Whether this transaction has been reviewed by the user
    /// </summary>
    public bool IsReviewed { get; set; } = false;

    /// <summary>
    /// Whether this transaction should be excluded from budgets/reports
    /// </summary>
    public bool IsExcluded { get; set; } = false;

    /// <summary>
    /// Tags for additional organization (comma-separated)
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    // Foreign keys
    /// <summary>
    /// ID of the account this transaction belongs to
    /// </summary>
    [Required]
    public int AccountId { get; set; }

    /// <summary>
    /// ID of the category this transaction is assigned to
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// ID of the related transaction (for transfers)
    /// </summary>
    public int? RelatedTransactionId { get; set; }

    /// <summary>
    /// Type of transaction (Income, Expense, TransferComponent)
    /// </summary>
    public TransactionType Type { get; set; } = TransactionType.Expense;

    /// <summary>
    /// Transfer ID if this transaction is part of a transfer
    /// </summary>
    public Guid? TransferId { get; set; }

    /// <summary>
    /// Whether this is the source side of a transfer (true = debit, false = credit)
    /// </summary>
    public bool IsTransferSource { get; set; } = false;

    // Auto-categorization tracking fields
    /// <summary>
    /// Whether this transaction was automatically categorized by the pipeline
    /// </summary>
    public bool IsAutoCategorized { get; set; } = false;

    /// <summary>
    /// Method used to automatically categorize this transaction
    /// </summary>
    [MaxLength(20)]
    public string? AutoCategorizationMethod { get; set; } // "Rule", "ML", "LLM", "Manual"

    /// <summary>
    /// Confidence score of the auto-categorization (0.0000 to 1.0000)
    /// </summary>
    public decimal? AutoCategorizationConfidence { get; set; }

    /// <summary>
    /// When this transaction was automatically categorized
    /// </summary>
    public DateTime? AutoCategorizedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// Account that this transaction belongs to
    /// </summary>
    public Account Account { get; set; } = null!;

    /// <summary>
    /// Category assigned to this transaction
    /// </summary>
    public Category? Category { get; set; }

    /// <summary>
    /// Related transaction (for transfers between accounts)
    /// </summary>
    public Transaction? RelatedTransaction { get; set; }

    /// <summary>
    /// Transfer that this transaction is part of (if applicable)
    /// </summary>
    public Transfer? Transfer { get; set; }

    /// <summary>
    /// Transaction splits if this transaction is split across categories
    /// </summary>
    public ICollection<TransactionSplit> Splits { get; set; } = new List<TransactionSplit>();

    /// <summary>
    /// Gets the display description, preferring user description over original
    /// </summary>
    public string GetDisplayDescription()
    {
        return !string.IsNullOrWhiteSpace(UserDescription) ? UserDescription : Description;
    }

    /// <summary>
    /// Gets the absolute amount (always positive)
    /// </summary>
    public decimal GetAbsoluteAmount()
    {
        return Math.Abs(Amount);
    }

    /// <summary>
    /// Checks if this is an income transaction (positive amount)
    /// </summary>
    public bool IsIncome()
    {
        return Amount > 0;
    }

    /// <summary>
    /// Checks if this is an expense transaction (negative amount)
    /// </summary>
    public bool IsExpense()
    {
        return Amount < 0;
    }

    /// <summary>
    /// Checks if this transaction is split across multiple categories
    /// </summary>
    public bool IsSplit()
    {
        return Splits.Any(s => !s.IsDeleted);
    }

    /// <summary>
    /// Gets the effective amount considering splits
    /// </summary>
    public decimal GetEffectiveAmount()
    {
        if (IsSplit())
        {
            return Splits.Where(s => !s.IsDeleted).Sum(s => s.Amount);
        }
        return Amount;
    }

    /// <summary>
    /// Marks this transaction as auto-categorized with the specified details
    /// </summary>
    public void MarkAsAutoCategorized(string method, decimal confidence, string appliedBy)
    {
        IsAutoCategorized = true;
        AutoCategorizationMethod = method;
        AutoCategorizationConfidence = confidence;
        AutoCategorizedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = appliedBy;
    }

    /// <summary>
    /// Checks if this transaction was auto-categorized by the specified method
    /// </summary>
    public bool WasAutoCategorizedBy(string method)
    {
        return IsAutoCategorized && AutoCategorizationMethod == method;
    }

    /// <summary>
    /// Parses tags from the Tags string
    /// </summary>
    public IEnumerable<string> GetTags()
    {
        if (string.IsNullOrWhiteSpace(Tags))
            return Enumerable.Empty<string>();

        return Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(tag => tag.Trim())
                  .Where(tag => !string.IsNullOrEmpty(tag));
    }

    /// <summary>
    /// Sets tags from a collection of strings
    /// </summary>
    public void SetTags(IEnumerable<string> tags)
    {
        Tags = string.Join(",", tags.Where(t => !string.IsNullOrWhiteSpace(t)));
    }

    /// <summary>
    /// Checks if this transaction is part of a transfer
    /// </summary>
    public bool IsTransfer()
    {
        return TransferId.HasValue || Type == TransactionType.TransferComponent;
    }

    /// <summary>
    /// Gets the transaction type based on amount if not explicitly set
    /// </summary>
    public TransactionType GetEffectiveType()
    {
        // If explicitly set as transfer component, return that
        if (Type == TransactionType.TransferComponent)
            return Type;

        // Otherwise, determine by amount
        return Amount >= 0 ? TransactionType.Income : TransactionType.Expense;
    }

    /// <summary>
    /// Sets the transaction type based on the amount
    /// </summary>
    public void SetTypeFromAmount()
    {
        if (Type != TransactionType.TransferComponent)
        {
            Type = Amount >= 0 ? TransactionType.Income : TransactionType.Expense;
        }
    }
}