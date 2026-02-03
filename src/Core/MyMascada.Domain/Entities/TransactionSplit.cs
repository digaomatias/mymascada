using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a split of a transaction across multiple categories.
/// Allows a single transaction to be divided into multiple categorized amounts.
/// </summary>
public class TransactionSplit : BaseEntity
{
    /// <summary>
    /// Amount for this split (should sum to parent transaction amount)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Optional description for this split
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Optional notes for this split
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    // Foreign keys
    /// <summary>
    /// ID of the parent transaction being split
    /// </summary>
    [Required]
    public int TransactionId { get; set; }

    /// <summary>
    /// ID of the category for this split
    /// </summary>
    [Required]
    public int CategoryId { get; set; }

    // Navigation properties
    /// <summary>
    /// Parent transaction that is being split
    /// </summary>
    public Transaction Transaction { get; set; } = null!;

    /// <summary>
    /// Category assigned to this split
    /// </summary>
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Gets the absolute amount (always positive)
    /// </summary>
    public decimal GetAbsoluteAmount()
    {
        return Math.Abs(Amount);
    }

    /// <summary>
    /// Gets the percentage of the parent transaction this split represents
    /// </summary>
    public decimal GetPercentageOfTransaction()
    {
        if (Transaction?.Amount == 0 || Transaction == null)
            return 0;

        return Math.Abs(Amount / Transaction.Amount) * 100;
    }
}