using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class CategorizationRuleRepository : ICategorizationRuleRepository
{
    private readonly ApplicationDbContext _context;

    public CategorizationRuleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CategorizationRule>> GetActiveRulesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.CategorizationRules
            .Include(r => r.Category)
            .Include(r => r.Conditions.Where(c => !c.IsDeleted))
            .Include(r => r.Applications)
            .Where(r => r.UserId == userId && r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CategorizationRule>> GetAllRulesForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.CategorizationRules
            .Include(r => r.Category)
            .Include(r => r.Conditions.Where(c => !c.IsDeleted))
            .Include(r => r.Applications)
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .OrderBy(r => r.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<CategorizationRule?> GetRuleByIdAsync(int ruleId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.CategorizationRules
            .Include(r => r.Category)
            .Include(r => r.Conditions.Where(c => !c.IsDeleted))
            .Include(r => r.Applications)
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.UserId == userId && !r.IsDeleted, cancellationToken);
    }

    public async Task<CategorizationRule> CreateRuleAsync(CategorizationRule rule, CancellationToken cancellationToken = default)
    {
        // Set creation timestamps
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;

        // Set condition timestamps
        foreach (var condition in rule.Conditions)
        {
            condition.CreatedAt = DateTime.UtcNow;
            condition.UpdatedAt = DateTime.UtcNow;
        }

        _context.CategorizationRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetRuleByIdAsync(rule.Id, rule.UserId, cancellationToken) 
               ?? throw new InvalidOperationException("Failed to reload created rule");
    }

    public async Task<CategorizationRule> UpdateRuleAsync(CategorizationRule rule, CancellationToken cancellationToken = default)
    {
        rule.UpdatedAt = DateTime.UtcNow;

        _context.CategorizationRules.Update(rule);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetRuleByIdAsync(rule.Id, rule.UserId, cancellationToken) 
               ?? throw new InvalidOperationException("Failed to reload updated rule");
    }

    public async Task DeleteRuleAsync(int ruleId, Guid userId, CancellationToken cancellationToken = default)
    {
        var rule = await _context.CategorizationRules
            .FirstOrDefaultAsync(r => r.Id == ruleId && r.UserId == userId && !r.IsDeleted, cancellationToken);

        if (rule != null)
        {
            rule.IsDeleted = true;
            rule.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<CategorizationRule>> GetMatchingRulesAsync(int transactionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Transactions
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.Account.UserId == userId, cancellationToken);

        if (transaction == null)
            return Enumerable.Empty<CategorizationRule>();

        var rules = await GetActiveRulesForUserAsync(userId, cancellationToken);
        var matchingRules = new List<CategorizationRule>();

        foreach (var rule in rules)
        {
            if (DoesRuleMatch(rule, transaction))
            {
                matchingRules.Add(rule);
            }
        }

        return matchingRules;
    }

    public async Task UpdateRulePrioritiesAsync(Guid userId, Dictionary<int, int> rulePriorities, CancellationToken cancellationToken = default)
    {
        var rules = await _context.CategorizationRules
            .Where(r => r.UserId == userId && !r.IsDeleted && rulePriorities.Keys.Contains(r.Id))
            .ToListAsync(cancellationToken);

        foreach (var rule in rules)
        {
            if (rulePriorities.TryGetValue(rule.Id, out var newPriority))
            {
                rule.Priority = newPriority;
                rule.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<int, (int MatchCount, int CorrectionCount, double AccuracyRate)>> GetRuleStatisticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rules = await _context.CategorizationRules
            .Where(r => r.UserId == userId && !r.IsDeleted)
            .Select(r => new
            {
                r.Id,
                r.MatchCount,
                r.CorrectionCount
            })
            .ToListAsync(cancellationToken);

        return rules.ToDictionary(
            r => r.Id,
            r => (
                MatchCount: r.MatchCount,
                CorrectionCount: r.CorrectionCount,
                AccuracyRate: r.MatchCount > 0 ? (double)(r.MatchCount - r.CorrectionCount) / r.MatchCount * 100 : 0
            )
        );
    }

    private static bool DoesRuleMatch(CategorizationRule rule, Transaction transaction)
    {
        // Check basic pattern matching
        var description = transaction.Description ?? "";
        var userDescription = transaction.UserDescription ?? "";
        var targetText = string.IsNullOrEmpty(userDescription) ? description : userDescription;

        var patternMatches = rule.Type switch
        {
            RuleType.Contains => rule.IsCaseSensitive 
                ? targetText.Contains(rule.Pattern) 
                : targetText.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleType.StartsWith => rule.IsCaseSensitive 
                ? targetText.StartsWith(rule.Pattern) 
                : targetText.StartsWith(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleType.EndsWith => rule.IsCaseSensitive 
                ? targetText.EndsWith(rule.Pattern) 
                : targetText.EndsWith(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleType.Equals => rule.IsCaseSensitive 
                ? targetText.Equals(rule.Pattern) 
                : targetText.Equals(rule.Pattern, StringComparison.OrdinalIgnoreCase),
            RuleType.Regex => System.Text.RegularExpressions.Regex.IsMatch(targetText, rule.Pattern, 
                rule.IsCaseSensitive ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            _ => false
        };

        if (!patternMatches)
            return false;

        // Check amount range
        if (rule.MinAmount.HasValue && Math.Abs(transaction.Amount) < rule.MinAmount.Value)
            return false;

        if (rule.MaxAmount.HasValue && Math.Abs(transaction.Amount) > rule.MaxAmount.Value)
            return false;

        // Check account types
        if (!string.IsNullOrEmpty(rule.AccountTypes))
        {
            var allowedTypes = rule.AccountTypes.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (allowedTypes.Length > 0 && !allowedTypes.Contains(transaction.Account.Type.ToString()))
                return false;
        }

        // Check advanced conditions if any
        if (rule.Conditions.Any())
        {
            var conditionResults = new List<bool>();

            foreach (var condition in rule.Conditions.Where(c => !c.IsDeleted))
            {
                var conditionValue = condition.Field switch
                {
                    RuleConditionField.Description => transaction.Description ?? "",
                    RuleConditionField.UserDescription => transaction.UserDescription ?? "",
                    RuleConditionField.Amount => transaction.Amount.ToString("F2"),
                    RuleConditionField.AccountType => transaction.Account.Type.ToString(),
                    RuleConditionField.AccountName => transaction.Account.Name ?? "",
                    RuleConditionField.TransactionType => transaction.Amount >= 0 ? "Income" : "Expense",
                    RuleConditionField.ReferenceNumber => transaction.ReferenceNumber ?? "",
                    RuleConditionField.Notes => transaction.Notes ?? "",
                    _ => ""
                };

                var conditionMatches = condition.Operator switch
                {
                    RuleConditionOperator.Equals => condition.IsCaseSensitive 
                        ? conditionValue.Equals(condition.Value) 
                        : conditionValue.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
                    RuleConditionOperator.NotEquals => condition.IsCaseSensitive 
                        ? !conditionValue.Equals(condition.Value) 
                        : !conditionValue.Equals(condition.Value, StringComparison.OrdinalIgnoreCase),
                    RuleConditionOperator.Contains => condition.IsCaseSensitive 
                        ? conditionValue.Contains(condition.Value) 
                        : conditionValue.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
                    RuleConditionOperator.NotContains => condition.IsCaseSensitive 
                        ? !conditionValue.Contains(condition.Value) 
                        : !conditionValue.Contains(condition.Value, StringComparison.OrdinalIgnoreCase),
                    RuleConditionOperator.StartsWith => condition.IsCaseSensitive 
                        ? conditionValue.StartsWith(condition.Value) 
                        : conditionValue.StartsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
                    RuleConditionOperator.EndsWith => condition.IsCaseSensitive 
                        ? conditionValue.EndsWith(condition.Value) 
                        : conditionValue.EndsWith(condition.Value, StringComparison.OrdinalIgnoreCase),
                    RuleConditionOperator.GreaterThan => decimal.TryParse(conditionValue, out var val1) && decimal.TryParse(condition.Value, out var val2) && val1 > val2,
                    RuleConditionOperator.LessThan => decimal.TryParse(conditionValue, out var val3) && decimal.TryParse(condition.Value, out var val4) && val3 < val4,
                    RuleConditionOperator.GreaterThanOrEqual => decimal.TryParse(conditionValue, out var val5) && decimal.TryParse(condition.Value, out var val6) && val5 >= val6,
                    RuleConditionOperator.LessThanOrEqual => decimal.TryParse(conditionValue, out var val7) && decimal.TryParse(condition.Value, out var val8) && val7 <= val8,
                    RuleConditionOperator.Regex => System.Text.RegularExpressions.Regex.IsMatch(conditionValue, condition.Value, 
                        condition.IsCaseSensitive ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    _ => false
                };

                conditionResults.Add(conditionMatches);
            }

            // Apply logic (All = AND, Any = OR)
            var allConditionsMatch = rule.Logic == RuleLogic.All 
                ? conditionResults.All(r => r) 
                : conditionResults.Any(r => r);

            if (!allConditionsMatch)
                return false;
        }

        return true;
    }
}