using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a sample transaction that supports a rule suggestion.
/// Used to show users examples of transactions that would match the suggested rule.
/// </summary>
public class RuleSuggestionSample : BaseEntity
{
    /// <summary>
    /// Transaction description that matches the rule pattern
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Transaction amount (for display purposes)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Date of the transaction
    /// </summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// Account name for display purposes
    /// </summary>
    [MaxLength(100)]
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Sort order for displaying samples (0 = primary example)
    /// </summary>
    public int SortOrder { get; set; }

    // Foreign keys
    /// <summary>
    /// ID of the rule suggestion this sample belongs to
    /// </summary>
    [Required]
    public int RuleSuggestionId { get; set; }

    /// <summary>
    /// ID of the original transaction this sample references
    /// </summary>
    [Required]
    public int TransactionId { get; set; }

    // Navigation properties
    /// <summary>
    /// The rule suggestion this sample belongs to
    /// </summary>
    public RuleSuggestion RuleSuggestion { get; set; } = null!;

    /// <summary>
    /// The original transaction this sample references
    /// </summary>
    public Transaction Transaction { get; set; } = null!;
}