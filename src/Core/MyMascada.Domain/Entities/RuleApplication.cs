using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents an instance of a rule being applied to a transaction
/// Used for auditing and rule performance tracking
/// </summary>
public class RuleApplication : BaseEntity
{
    /// <summary>
    /// ID of the rule that was applied
    /// </summary>
    [Required]
    public int RuleId { get; set; }

    /// <summary>
    /// ID of the transaction the rule was applied to
    /// </summary>
    [Required]
    public int TransactionId { get; set; }

    /// <summary>
    /// ID of the category that was assigned by the rule
    /// </summary>
    [Required]
    public int CategoryId { get; set; }

    /// <summary>
    /// Confidence score at the time of application (0.0 to 1.0)
    /// </summary>
    public decimal ConfidenceScore { get; set; } = 0.8m;

    /// <summary>
    /// Whether this application was later corrected by the user
    /// </summary>
    public bool WasCorrected { get; set; } = false;

    /// <summary>
    /// If corrected, the new category ID chosen by the user
    /// </summary>
    public int? CorrectedCategoryId { get; set; }

    /// <summary>
    /// When the correction was made (if applicable)
    /// </summary>
    public DateTime? CorrectedAt { get; set; }

    /// <summary>
    /// How the rule was triggered (automatic, manual, suggestion accepted, etc.)
    /// </summary>
    [MaxLength(50)]
    public string TriggerSource { get; set; } = "Automatic";

    /// <summary>
    /// Additional metadata about the rule application
    /// </summary>
    [MaxLength(1000)]
    public string? Metadata { get; set; }

    // Navigation properties
    /// <summary>
    /// The rule that was applied
    /// </summary>
    public CategorizationRule Rule { get; set; } = null!;

    /// <summary>
    /// The transaction the rule was applied to
    /// </summary>
    public Transaction Transaction { get; set; } = null!;

    /// <summary>
    /// The category that was assigned
    /// </summary>
    public Category Category { get; set; } = null!;

    /// <summary>
    /// The corrected category (if applicable)
    /// </summary>
    public Category? CorrectedCategory { get; set; }

    /// <summary>
    /// Records a correction made by the user
    /// </summary>
    public void RecordCorrection(int newCategoryId)
    {
        WasCorrected = true;
        CorrectedCategoryId = newCategoryId;
        CorrectedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    }

    /// <summary>
    /// Checks if this application was successful (not corrected)
    /// </summary>
    public bool IsSuccessful()
    {
        return !WasCorrected;
    }

    /// <summary>
    /// Gets the final category ID (corrected category if available, otherwise original)
    /// </summary>
    public int GetFinalCategoryId()
    {
        return CorrectedCategoryId ?? CategoryId;
    }
}