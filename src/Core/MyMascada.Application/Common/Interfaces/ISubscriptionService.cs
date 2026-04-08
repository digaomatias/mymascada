using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for checking subscription tier and AI feature quotas.
/// Self-hosted deployments (IFeatureFlags.StripeBilling == false) get unlimited access.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Whether the user's tier allows LLM-based transaction categorization.
    /// </summary>
    Task<bool> CanUseLlmCategorizationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Whether the user's tier allows AI-enhanced rule suggestion generation.
    /// </summary>
    Task<bool> CanUseAiRuleSuggestionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Remaining LLM categorization quota for the current month.
    /// Returns int.MaxValue for unlimited tiers.
    /// </summary>
    Task<int> GetRemainingLlmQuotaAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Remaining AI rule suggestion quota for the current month.
    /// Returns int.MaxValue for unlimited tiers.
    /// </summary>
    Task<int> GetRemainingRuleSuggestionQuotaAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Records LLM categorization usage for quota tracking.
    /// </summary>
    Task RecordLlmUsageAsync(Guid userId, int transactionCount, CancellationToken ct = default);

    /// <summary>
    /// Records one AI rule suggestion generation for quota tracking.
    /// </summary>
    Task RecordRuleSuggestionUsageAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the user's current subscription tier.
    /// </summary>
    Task<SubscriptionTier> GetUserTierAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Whether the deployment is self-hosted (StripeBilling disabled).
    /// </summary>
    Task<bool> IsSelfHostedAsync(CancellationToken ct = default);
}
