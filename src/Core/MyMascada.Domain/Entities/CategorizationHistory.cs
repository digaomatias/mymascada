using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Records a user's categorization pattern: a normalized description mapped to a category.
/// Used by the ML handler's similarity matching engine to categorize future transactions
/// based on the user's own categorization history.
/// </summary>
public class CategorizationHistory : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Normalized form of the transaction description (lowercase, stripped of dates/refs/trailing numbers).
    /// Together with UserId, forms the unique key for this mapping.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string NormalizedDescription { get; set; } = string.Empty;

    /// <summary>
    /// Original (un-normalized) description from the first transaction that created this mapping.
    /// Kept for human-readable display and debugging.
    /// </summary>
    [MaxLength(500)]
    public string OriginalDescription { get; set; } = string.Empty;

    /// <summary>
    /// The category this description maps to.
    /// </summary>
    [Required]
    public int CategoryId { get; set; }

    /// <summary>
    /// How many times this exact (userId, normalizedDescription, categoryId) mapping has been confirmed.
    /// Drives confidence scaling: 1st=0.70, 2nd=0.80, 3rd=0.85, 5+=0.90, 10+=0.95.
    /// </summary>
    public int MatchCount { get; set; } = 1;

    /// <summary>
    /// When this mapping was last used to categorize a transaction.
    /// </summary>
    public DateTime LastUsedAt { get; set; }

    /// <summary>
    /// How this mapping was originally created: Manual, CandidateApproved, RuleApplied, Backfill.
    /// </summary>
    [MaxLength(20)]
    public string Source { get; set; } = "Manual";

    // Navigation properties
    public Category Category { get; set; } = null!;
}

/// <summary>
/// Constants for CategorizationHistory source values.
/// </summary>
public static class CategorizationHistorySource
{
    public const string Manual = "Manual";
    public const string CandidateApproved = "CandidateApproved";
    public const string RuleApplied = "RuleApplied";
    public const string Backfill = "Backfill";
}
