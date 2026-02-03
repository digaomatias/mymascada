using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents an AI-generated suggestion for creating a categorization rule.
/// Used to analyze transaction patterns and suggest automation rules to users.
/// </summary>
public class RuleSuggestion : BaseEntity
{
    /// <summary>
    /// Name/title of the suggested rule
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description explaining what this rule suggestion does
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Pattern to match against transaction descriptions
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Type of pattern matching (Contains, StartsWith, etc.)
    /// </summary>
    public RuleType Type { get; set; } = RuleType.Contains;

    /// <summary>
    /// Whether the pattern matching should be case sensitive
    /// </summary>
    public bool IsCaseSensitive { get; set; } = false;

    /// <summary>
    /// AI confidence score for this suggestion (0.0 to 1.0)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Number of transactions that would match this rule
    /// </summary>
    public int MatchCount { get; set; }

    /// <summary>
    /// Method used to generate this suggestion (e.g., "Basic Pattern Analysis", "AI Analysis")
    /// </summary>
    [MaxLength(100)]
    public string GenerationMethod { get; set; } = "Basic Pattern Analysis";

    /// <summary>
    /// Whether this suggestion has been accepted by the user
    /// </summary>
    public bool IsAccepted { get; set; } = false;

    /// <summary>
    /// Whether this suggestion has been rejected/dismissed by the user
    /// </summary>
    public bool IsRejected { get; set; } = false;

    /// <summary>
    /// Date when this suggestion was accepted or rejected
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// ID of the rule created from this suggestion (if accepted)
    /// </summary>
    public int? CreatedRuleId { get; set; }

    // Foreign keys
    /// <summary>
    /// User ID who owns this suggestion
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Suggested category ID for this rule
    /// </summary>
    [Required]
    public int SuggestedCategoryId { get; set; }

    // Navigation properties
    /// <summary>
    /// Suggested category for this rule
    /// </summary>
    public Category SuggestedCategory { get; set; } = null!;

    /// <summary>
    /// Rule created from this suggestion (if accepted)
    /// </summary>
    public CategorizationRule? CreatedRule { get; set; }

    /// <summary>
    /// Sample transactions that support this suggestion
    /// </summary>
    public ICollection<RuleSuggestionSample> SampleTransactions { get; set; } = new List<RuleSuggestionSample>();

    /// <summary>
    /// Marks this suggestion as accepted and records the created rule
    /// </summary>
    public void Accept(int createdRuleId)
    {
        IsAccepted = true;
        IsRejected = false;
        ProcessedAt = DateTime.UtcNow;
        CreatedRuleId = createdRuleId;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks this suggestion as rejected/dismissed
    /// </summary>
    public void Reject()
    {
        IsRejected = true;
        IsAccepted = false;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this suggestion is still pending (not accepted or rejected)
    /// </summary>
    public bool IsPending => !IsAccepted && !IsRejected;

    /// <summary>
    /// Gets the confidence percentage as an integer (0-100)
    /// </summary>
    public int GetConfidencePercentage()
    {
        return (int)Math.Round(ConfidenceScore * 100);
    }

    /// <summary>
    /// Gets the primary sample transaction description for display
    /// </summary>
    public string? GetPrimarySampleDescription()
    {
        return SampleTransactions.OrderBy(s => s.SortOrder).FirstOrDefault()?.Description;
    }

    /// <summary>
    /// Adds a sample transaction to support this suggestion
    /// </summary>
    public void AddSampleTransaction(int transactionId, string description, decimal amount, DateTime transactionDate, string accountName)
    {
        var sample = new RuleSuggestionSample
        {
            RuleSuggestionId = Id,
            TransactionId = transactionId,
            Description = description,
            Amount = amount,
            TransactionDate = transactionDate,
            AccountName = accountName,
            SortOrder = SampleTransactions.Count
        };

        SampleTransactions.Add(sample);
    }
}