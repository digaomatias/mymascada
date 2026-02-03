using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a single condition within a categorization rule
/// Supports complex multi-condition rules
/// </summary>
public class RuleCondition : BaseEntity
{
    /// <summary>
    /// The field to match against (Description, Amount, AccountType, etc.)
    /// </summary>
    public RuleConditionField Field { get; set; }

    /// <summary>
    /// The comparison operation to perform
    /// </summary>
    public RuleConditionOperator Operator { get; set; }

    /// <summary>
    /// The value to compare against
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Whether the comparison should be case sensitive (for text fields)
    /// </summary>
    public bool IsCaseSensitive { get; set; } = false;

    /// <summary>
    /// Order of this condition within the rule (for display purposes)
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// ID of the rule this condition belongs to
    /// </summary>
    [Required]
    public int RuleId { get; set; }

    // Navigation properties
    /// <summary>
    /// The rule this condition belongs to
    /// </summary>
    public CategorizationRule Rule { get; set; } = null!;

    /// <summary>
    /// Evaluates this condition against a transaction
    /// </summary>
    public bool Evaluate(Transaction transaction)
    {
        var actualValue = GetFieldValue(transaction);
        return CompareValues(actualValue, Value, Operator, IsCaseSensitive);
    }

    /// <summary>
    /// Gets the actual field value from the transaction based on the field type
    /// </summary>
    private string GetFieldValue(Transaction transaction)
    {
        return Field switch
        {
            RuleConditionField.Description => transaction.Description ?? string.Empty,
            RuleConditionField.UserDescription => transaction.UserDescription ?? string.Empty,
            RuleConditionField.Amount => Math.Abs(transaction.Amount).ToString("F2"),
            RuleConditionField.AccountType => transaction.Account.Type.ToString(),
            RuleConditionField.AccountName => transaction.Account.Name ?? string.Empty,
            RuleConditionField.TransactionType => transaction.Type.ToString(),
            RuleConditionField.ReferenceNumber => transaction.ReferenceNumber ?? string.Empty,
            RuleConditionField.Notes => transaction.Notes ?? string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Compares two values using the specified operator
    /// </summary>
    private static bool CompareValues(string actualValue, string expectedValue, RuleConditionOperator op, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return op switch
        {
            RuleConditionOperator.Equals => actualValue.Equals(expectedValue, comparison),
            RuleConditionOperator.NotEquals => !actualValue.Equals(expectedValue, comparison),
            RuleConditionOperator.Contains => actualValue.Contains(expectedValue, comparison),
            RuleConditionOperator.NotContains => !actualValue.Contains(expectedValue, comparison),
            RuleConditionOperator.StartsWith => actualValue.StartsWith(expectedValue, comparison),
            RuleConditionOperator.EndsWith => actualValue.EndsWith(expectedValue, comparison),
            RuleConditionOperator.GreaterThan => decimal.TryParse(actualValue, out var actual1) && 
                                                decimal.TryParse(expectedValue, out var expected1) && 
                                                actual1 > expected1,
            RuleConditionOperator.LessThan => decimal.TryParse(actualValue, out var actual2) && 
                                              decimal.TryParse(expectedValue, out var expected2) && 
                                              actual2 < expected2,
            RuleConditionOperator.GreaterThanOrEqual => decimal.TryParse(actualValue, out var actual3) && 
                                                       decimal.TryParse(expectedValue, out var expected3) && 
                                                       actual3 >= expected3,
            RuleConditionOperator.LessThanOrEqual => decimal.TryParse(actualValue, out var actual4) && 
                                                    decimal.TryParse(expectedValue, out var expected4) && 
                                                    actual4 <= expected4,
            RuleConditionOperator.Regex => TryRegexMatch(actualValue, expectedValue),
            _ => false
        };
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