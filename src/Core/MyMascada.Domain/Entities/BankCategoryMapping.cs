using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a mapping between a bank-provided category (e.g., from Akahu)
/// and a user's MyMascada category.
/// Enables automatic categorization of transactions based on bank category data.
/// </summary>
public class BankCategoryMapping : BaseEntity
{
    /// <summary>
    /// The original bank category name as received from the provider.
    /// Example: "Supermarket and groceries", "Online Shopping"
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string BankCategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Normalized version of the bank category name for consistent matching.
    /// Lowercase, trimmed, used for lookups.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>
    /// Bank provider identifier (e.g., "akahu").
    /// Allows different mappings for different providers.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// User who owns this mapping.
    /// Each user has their own category structure and mappings.
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Target MyMascada category ID.
    /// </summary>
    [Required]
    public int CategoryId { get; set; }

    /// <summary>
    /// Confidence/trust score for this mapping (0.0 to 1.0).
    /// Higher values indicate more reliable mappings:
    /// - 1.0: User-confirmed mapping
    /// - 0.9+: High-confidence AI mapping
    /// - 0.7-0.9: Auto-learned from user behavior
    /// - Below 0.7: Low-confidence, requires review
    /// </summary>
    public decimal ConfidenceScore { get; set; } = 0.8m;

    /// <summary>
    /// How this mapping was created.
    /// "AI" - Created by AI analysis
    /// "User" - Manually created by user
    /// "Learned" - Auto-learned from user categorization
    /// </summary>
    [MaxLength(20)]
    public string Source { get; set; } = "AI";

    /// <summary>
    /// Number of times this mapping has been successfully applied.
    /// Used to track mapping effectiveness and adjust confidence.
    /// </summary>
    public int ApplicationCount { get; set; } = 0;

    /// <summary>
    /// Number of times the user overrode this mapping with a different category.
    /// High override count may indicate the mapping needs adjustment.
    /// </summary>
    public int OverrideCount { get; set; } = 0;

    /// <summary>
    /// Whether this mapping is currently active and should be used.
    /// Allows disabling mappings without deleting them.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this bank category should be excluded from automatic categorization.
    /// When true, transactions with this bank category will not be auto-categorized
    /// and will be passed to the next handler in the pipeline (ML, LLM, or left uncategorized).
    /// Useful for categories like "Lending Services" that may misrepresent transaction types.
    /// </summary>
    public bool IsExcluded { get; set; } = false;

    // Navigation property
    /// <summary>
    /// The MyMascada category this bank category maps to.
    /// </summary>
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Creates a normalized name from the bank category name.
    /// </summary>
    public static string Normalize(string bankCategoryName)
    {
        return bankCategoryName?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    /// <summary>
    /// Calculates the effective confidence score based on application and override history.
    /// </summary>
    public decimal GetEffectiveConfidence()
    {
        if (ApplicationCount == 0 && OverrideCount == 0)
            return ConfidenceScore;

        var totalInteractions = ApplicationCount + OverrideCount;
        if (totalInteractions == 0)
            return ConfidenceScore;

        // Reduce confidence based on override ratio
        var overrideRatio = (decimal)OverrideCount / totalInteractions;
        var adjustedConfidence = ConfidenceScore * (1 - overrideRatio * 0.5m);

        return Math.Max(0.1m, Math.Min(1.0m, adjustedConfidence));
    }
}
