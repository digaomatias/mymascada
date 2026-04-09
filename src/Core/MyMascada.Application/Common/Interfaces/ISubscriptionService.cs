using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Result of an AI feature access check, including the user's tier, denial reason, and remaining quota.
/// RemainingQuota is int.MaxValue for unlimited tiers, 0 when denied, or the actual remaining count.
/// </summary>
public record AiFeatureAccessResult(bool IsAllowed, SubscriptionTier Tier, string? DenialReason = null, int RemainingQuota = 0);

/// <summary>
/// Service for checking subscription tier and AI feature quotas.
/// Self-hosted deployments (IFeatureFlags.StripeBilling == false) get unlimited access.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Checks whether the user's tier allows LLM-based transaction categorization.
    /// Returns tier and denial reason to avoid redundant lookups.
    /// </summary>
    Task<AiFeatureAccessResult> CanUseLlmCategorizationAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the user's tier allows AI-enhanced rule suggestion generation.
    /// Returns tier and denial reason to avoid redundant lookups.
    /// </summary>
    Task<AiFeatureAccessResult> CanUseAiRuleSuggestionsAsync(Guid userId, CancellationToken ct = default);

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
    /// Atomically checks whether the user can use AI rule suggestions and reserves one usage slot.
    /// Returns true if the reservation succeeded (quota was available and has been decremented).
    /// This prevents concurrent requests from both passing the quota check.
    /// </summary>
    Task<bool> TryReserveRuleSuggestionQuotaAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the user's current subscription tier.
    /// </summary>
    Task<SubscriptionTier> GetUserTierAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Whether the deployment is self-hosted (StripeBilling disabled).
    /// </summary>
    Task<bool> IsSelfHostedAsync(CancellationToken ct = default);
}
