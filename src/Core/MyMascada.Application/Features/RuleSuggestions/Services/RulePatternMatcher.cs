using System.Text.RegularExpressions;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Shared utility for testing whether a categorization rule's pattern matches a description.
/// Single source of truth — used by RuleSuggestionService and CategorizationHistoryAnalyzer.
/// </summary>
public static class RulePatternMatcher
{
    public static bool Matches(CategorizationRule rule, string description)
    {
        if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(rule.Pattern))
            return false;

        var comparison = rule.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return rule.Type switch
        {
            RuleType.Contains => description.Contains(rule.Pattern, comparison),
            RuleType.StartsWith => description.StartsWith(rule.Pattern, comparison),
            RuleType.EndsWith => description.EndsWith(rule.Pattern, comparison),
            RuleType.Equals => description.Equals(rule.Pattern, comparison),
            RuleType.Regex => SafeRegexMatch(description, rule.Pattern, rule.IsCaseSensitive),
            _ => false
        };
    }

    private static bool SafeRegexMatch(string input, string pattern, bool caseSensitive)
    {
        try
        {
            var options = caseSensitive
                ? RegexOptions.None
                : RegexOptions.IgnoreCase;
            return Regex.IsMatch(input, pattern, options, TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            return false;
        }
    }
}
