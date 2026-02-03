using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a suggested categorization for a transaction that requires user approval
/// Supports multiple suggestions per transaction for comprehensive categorization options
/// </summary>
public class CategorizationCandidate : BaseEntity
{
    /// <summary>
    /// ID of the transaction this suggestion applies to
    /// </summary>
    [Required]
    public int TransactionId { get; set; }

    /// <summary>
    /// ID of the suggested category
    /// </summary>
    [Required]
    public int CategoryId { get; set; }

    /// <summary>
    /// Method used to generate this suggestion
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string CategorizationMethod { get; set; } = string.Empty; // "Rule", "ML", "LLM"

    /// <summary>
    /// Confidence score of this suggestion (0.0000 to 1.0000)
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Name of the specific handler that processed this suggestion
    /// </summary>
    [MaxLength(50)]
    public string? ProcessedBy { get; set; }

    /// <summary>
    /// Human-readable explanation of why this category was suggested
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// Additional metadata as JSON (rule matched, ML features, LLM context, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Current status of this candidate
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = CandidateStatus.Pending; // "Pending", "Applied", "Rejected"

    /// <summary>
    /// When this candidate was applied to the transaction (if applied)
    /// </summary>
    public DateTime? AppliedAt { get; set; }

    /// <summary>
    /// User ID who applied or rejected this candidate
    /// </summary>
    [MaxLength(50)]
    public string? AppliedBy { get; set; }

    // Navigation properties
    /// <summary>
    /// Transaction this candidate applies to
    /// </summary>
    public Transaction Transaction { get; set; } = null!;

    /// <summary>
    /// Suggested category
    /// </summary>
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Checks if this candidate can be auto-applied based on confidence and method
    /// </summary>
    public bool CanAutoApply(decimal autoApplyThreshold = 0.95m)
    {
        // Rules and BankCategory with very high confidence should auto-apply
        return (CategorizationMethod == CandidateMethod.Rule ||
                CategorizationMethod == CandidateMethod.BankCategory) &&
               ConfidenceScore >= autoApplyThreshold;
    }

    /// <summary>
    /// Marks this candidate as applied
    /// </summary>
    public void MarkAsApplied(string appliedBy)
    {
        Status = CandidateStatus.Applied;
        AppliedAt = DateTime.UtcNow;
        AppliedBy = appliedBy?.Length > 50 ? appliedBy.Substring(0, 50) : appliedBy;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = appliedBy?.Length > 50 ? appliedBy.Substring(0, 50) : appliedBy;
    }

    /// <summary>
    /// Marks this candidate as rejected
    /// </summary>
    public void MarkAsRejected(string rejectedBy)
    {
        Status = CandidateStatus.Rejected;
        AppliedAt = DateTime.UtcNow;
        AppliedBy = rejectedBy?.Length > 50 ? rejectedBy.Substring(0, 50) : rejectedBy;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = rejectedBy?.Length > 50 ? rejectedBy.Substring(0, 50) : rejectedBy;
    }
}

/// <summary>
/// Constants for candidate status values
/// </summary>
public static class CandidateStatus
{
    public const string Pending = "Pending";
    public const string Applied = "Applied";
    public const string Rejected = "Rejected";
}

/// <summary>
/// Constants for categorization method values
/// </summary>
public static class CandidateMethod
{
    public const string Rule = "Rule";
    public const string BankCategory = "BankCategory";
    public const string ML = "ML";
    public const string LLM = "LLM";
    public const string Manual = "Manual";
}