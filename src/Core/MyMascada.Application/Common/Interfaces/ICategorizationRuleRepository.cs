using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing categorization rules
/// </summary>
public interface ICategorizationRuleRepository
{
    /// <summary>
    /// Gets all active rules for a user, ordered by priority
    /// </summary>
    Task<IEnumerable<CategorizationRule>> GetActiveRulesForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all rules for a user (including inactive ones)
    /// </summary>
    Task<IEnumerable<CategorizationRule>> GetAllRulesForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific rule by ID for a user
    /// </summary>
    Task<CategorizationRule?> GetRuleByIdAsync(int ruleId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new rule
    /// </summary>
    Task<CategorizationRule> CreateRuleAsync(CategorizationRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing rule
    /// </summary>
    Task<CategorizationRule> UpdateRuleAsync(CategorizationRule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a rule (soft delete)
    /// </summary>
    Task DeleteRuleAsync(int ruleId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets rules that match a specific transaction for testing purposes
    /// </summary>
    Task<IEnumerable<CategorizationRule>> GetMatchingRulesAsync(int transactionId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates rule priority order for a user
    /// </summary>
    Task UpdateRulePrioritiesAsync(Guid userId, Dictionary<int, int> rulePriorities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets rule performance statistics
    /// </summary>
    Task<Dictionary<int, (int MatchCount, int CorrectionCount, double AccuracyRate)>> GetRuleStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);
}