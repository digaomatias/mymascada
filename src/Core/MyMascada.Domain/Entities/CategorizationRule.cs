using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a rule for automatically categorizing transactions.
/// Supports both user-defined rules and AI-learned patterns.
/// </summary>
public class CategorizationRule : BaseEntity
{
    /// <summary>
    /// Name of the rule for identification
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this rule does
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Type of rule (keyword matching, pattern matching, etc.)
    /// </summary>
    public RuleType Type { get; set; } = RuleType.Contains;

    /// <summary>
    /// Pattern or keyword to match against transaction descriptions
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Whether the pattern matching should be case sensitive
    /// </summary>
    public bool IsCaseSensitive { get; set; } = false;

    /// <summary>
    /// Priority of this rule (higher numbers processed first)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Whether this rule is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this rule was created by AI learning or user-defined
    /// </summary>
    public bool IsAiGenerated { get; set; } = false;

    /// <summary>
    /// Confidence score for AI-generated rules (0.0 to 1.0)
    /// </summary>
    public double? ConfidenceScore { get; set; }

    /// <summary>
    /// Number of times this rule has been successfully applied
    /// </summary>
    public int MatchCount { get; set; } = 0;

    /// <summary>
    /// Number of times this rule was applied but later corrected by user
    /// </summary>
    public int CorrectionCount { get; set; } = 0;

    /// <summary>
    /// Minimum transaction amount for this rule to apply (optional)
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Maximum transaction amount for this rule to apply (optional)
    /// </summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>
    /// Account types this rule applies to (null = all types)
    /// </summary>
    public string? AccountTypes { get; set; }

    // Foreign keys
    /// <summary>
    /// User ID who owns this rule
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Category ID to assign when this rule matches
    /// </summary>
    [Required]
    public int CategoryId { get; set; }

    // Navigation properties
    /// <summary>
    /// Category to assign when this rule matches
    /// </summary>
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Collection of advanced conditions for this rule (if using advanced mode)
    /// </summary>
    public ICollection<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();

    /// <summary>
    /// Collection of rule applications for auditing
    /// </summary>
    public ICollection<RuleApplication> Applications { get; set; } = new List<RuleApplication>();

    /// <summary>
    /// How multiple conditions should be combined (All = AND, Any = OR)
    /// </summary>
    public RuleLogic Logic { get; set; } = RuleLogic.All;

    /// <summary>
    /// Checks if this rule matches the given transaction description
    /// </summary>
    public bool MatchesDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var comparison = IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return Type switch
        {
            RuleType.Contains => description.Contains(Pattern, comparison),
            RuleType.StartsWith => description.StartsWith(Pattern, comparison),
            RuleType.EndsWith => description.EndsWith(Pattern, comparison),
            RuleType.Equals => description.Equals(Pattern, comparison),
            RuleType.Regex => TryRegexMatch(description, Pattern),
            _ => false
        };
    }

    /// <summary>
    /// Checks if this rule applies to the given transaction amount
    /// </summary>
    public bool MatchesAmount(decimal amount)
    {
        var absoluteAmount = Math.Abs(amount);

        if (MinAmount.HasValue && absoluteAmount < MinAmount.Value)
            return false;

        if (MaxAmount.HasValue && absoluteAmount > MaxAmount.Value)
            return false;

        return true;
    }

    /// <summary>
    /// Checks if this rule applies to the given account type
    /// </summary>
    public bool MatchesAccountType(AccountType accountType)
    {
        if (string.IsNullOrWhiteSpace(AccountTypes))
            return true; // Rule applies to all account types

        var allowedTypes = AccountTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(t => t.Trim());

        return allowedTypes.Contains(accountType.ToString(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if this rule fully matches the given transaction
    /// </summary>
    public bool Matches(Transaction transaction)
    {
        if (!IsActive)
            return false;

        // If rule has advanced conditions, use those instead of legacy pattern matching
        if (HasAdvancedConditions())
        {
            return EvaluateAdvancedConditions(transaction);
        }

        // Fallback to legacy pattern-based matching
        return MatchesDescription(transaction.Description) &&
               MatchesAmount(transaction.Amount) &&
               MatchesAccountType(transaction.Account.Type);
    }

    /// <summary>
    /// Checks if this rule has advanced conditions defined
    /// </summary>
    public bool HasAdvancedConditions()
    {
        return Conditions.Any(c => !c.IsDeleted);
    }

    /// <summary>
    /// Evaluates advanced conditions against the transaction
    /// </summary>
    private bool EvaluateAdvancedConditions(Transaction transaction)
    {
        var activeConditions = Conditions.Where(c => !c.IsDeleted).ToList();
        
        if (!activeConditions.Any())
            return false;

        return Logic switch
        {
            RuleLogic.All => activeConditions.All(condition => condition.Evaluate(transaction)),
            RuleLogic.Any => activeConditions.Any(condition => condition.Evaluate(transaction)),
            _ => false
        };
    }

    /// <summary>
    /// Gets the accuracy rate of this rule (successful matches / total matches)
    /// </summary>
    public double GetAccuracyRate()
    {
        var totalMatches = MatchCount + CorrectionCount;
        if (totalMatches == 0)
            return 1.0; // No data yet, assume perfect

        return (double)MatchCount / totalMatches;
    }

    /// <summary>
    /// Records a successful match for this rule
    /// </summary>
    public void RecordSuccessfulMatch()
    {
        MatchCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a correction (rule was wrong) for this rule
    /// </summary>
    public void RecordCorrection()
    {
        CorrectionCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the account types this rule applies to
    /// </summary>
    public IEnumerable<AccountType> GetAccountTypes()
    {
        if (string.IsNullOrWhiteSpace(AccountTypes))
            return Enum.GetValues<AccountType>();

        return AccountTypes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(t => t.Trim())
                          .Where(t => Enum.TryParse<AccountType>(t, true, out _))
                          .Select(t => Enum.Parse<AccountType>(t, true));
    }

    /// <summary>
    /// Sets the account types this rule applies to
    /// </summary>
    public void SetAccountTypes(IEnumerable<AccountType> accountTypes)
    {
        AccountTypes = string.Join(",", accountTypes.Select(t => t.ToString()));
    }

    /// <summary>
    /// Records that this rule was applied to a transaction
    /// </summary>
    public RuleApplication RecordApplication(int transactionId, int categoryId, decimal confidenceScore = 0.8m, string triggerSource = "Automatic")
    {
        var application = new RuleApplication
        {
            RuleId = Id,
            TransactionId = transactionId,
            CategoryId = categoryId,
            ConfidenceScore = confidenceScore,
            TriggerSource = triggerSource,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

        Applications.Add(application);
        RecordSuccessfulMatch();
        
        return application;
    }

    /// <summary>
    /// Gets the success rate of this rule based on applications
    /// </summary>
    public double GetSuccessRate()
    {
        var totalApplications = Applications.Count;
        if (totalApplications == 0)
            return 1.0; // No data yet, assume perfect

        var successfulApplications = Applications.Count(a => !a.WasCorrected);
        return (double)successfulApplications / totalApplications;
    }

    /// <summary>
    /// Safely attempts to match a regular expression, returning false if the pattern is invalid
    /// </summary>
    private static bool TryRegexMatch(string actualValue, string pattern)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(actualValue, pattern);
        }
        catch
        {
            return false; // Invalid regex pattern
        }
    }
}