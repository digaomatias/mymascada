using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services.Subscription;

/// <summary>
/// Resolves subscription tier from UserSubscription + BillingPlan and tracks AI usage quotas.
/// Self-hosted detection: when IFeatureFlags.StripeBilling is false, returns SelfHosted with unlimited quotas.
/// </summary>
public class SubscriptionService : ISubscriptionService
{
    private const int ProLlmQuotaPerMonth = 200;
    private const int ProRuleSuggestionQuotaPerMonth = 5;

    private readonly ApplicationDbContext _dbContext;
    private readonly IFeatureFlags _featureFlags;
    private readonly ILogger<SubscriptionService> _logger;

    // Per-request cache: avoids redundant DB queries when multiple methods
    // are called for the same user within a single scoped lifetime.
    private Guid? _cachedUserId;
    private UserSubscription? _cachedSubscription;

    public SubscriptionService(
        ApplicationDbContext dbContext,
        IFeatureFlags featureFlags,
        ILogger<SubscriptionService> logger)
    {
        _dbContext = dbContext;
        _featureFlags = featureFlags;
        _logger = logger;
    }

    public Task<bool> IsSelfHostedAsync(CancellationToken ct = default)
    {
        return Task.FromResult(!_featureFlags.StripeBilling);
    }

    public async Task<SubscriptionTier> GetUserTierAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_featureFlags.StripeBilling)
            return SubscriptionTier.SelfHosted;

        var subscription = await GetUserSubscriptionAsync(userId, ct);
        return ResolveTier(subscription);
    }

    public async Task<AiFeatureAccessResult> CanUseLlmCategorizationAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_featureFlags.StripeBilling)
            return new AiFeatureAccessResult(true, SubscriptionTier.SelfHosted, RemainingQuota: int.MaxValue);

        var subscription = await GetUserSubscriptionAsync(userId, ct);
        var tier = ResolveTier(subscription);

        if (tier == SubscriptionTier.Free)
            return new AiFeatureAccessResult(false, tier, "Subscription.LlmDeniedFreeTier");

        var quota = ResolvePlanLlmQuota(subscription);
        var usage = await GetCurrentMonthUsageAsync(userId, ct);
        var remaining = Math.Max(0, quota - usage.LlmCategorizationCount);

        if (remaining <= 0)
            return new AiFeatureAccessResult(false, tier, "Subscription.LlmQuotaExhausted");

        return new AiFeatureAccessResult(true, tier, RemainingQuota: remaining);
    }

    public async Task<AiFeatureAccessResult> CanUseAiRuleSuggestionsAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_featureFlags.StripeBilling)
            return new AiFeatureAccessResult(true, SubscriptionTier.SelfHosted, RemainingQuota: int.MaxValue);

        var subscription = await GetUserSubscriptionAsync(userId, ct);
        var tier = ResolveTier(subscription);

        if (tier == SubscriptionTier.Free)
            return new AiFeatureAccessResult(false, tier, "Subscription.AiRulesDeniedFreeTier");

        var usage = await GetCurrentMonthUsageAsync(userId, ct);
        var remaining = Math.Max(0, ProRuleSuggestionQuotaPerMonth - usage.RuleSuggestionCount);

        if (remaining <= 0)
            return new AiFeatureAccessResult(false, tier, "Subscription.AiRulesQuotaExhausted");

        return new AiFeatureAccessResult(true, tier, RemainingQuota: remaining);
    }

    public async Task<int> GetRemainingLlmQuotaAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_featureFlags.StripeBilling)
            return int.MaxValue;

        var subscription = await GetUserSubscriptionAsync(userId, ct);
        var tier = ResolveTier(subscription);

        if (tier == SubscriptionTier.Free)
            return 0;

        var quota = ResolvePlanLlmQuota(subscription);
        var usage = await GetCurrentMonthUsageAsync(userId, ct);
        return Math.Max(0, quota - usage.LlmCategorizationCount);
    }

    public async Task<int> GetRemainingRuleSuggestionQuotaAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_featureFlags.StripeBilling)
            return int.MaxValue;

        var subscription = await GetUserSubscriptionAsync(userId, ct);
        var tier = ResolveTier(subscription);

        if (tier == SubscriptionTier.Free)
            return 0;

        var usage = await GetCurrentMonthUsageAsync(userId, ct);
        return Math.Max(0, ProRuleSuggestionQuotaPerMonth - usage.RuleSuggestionCount);
    }

    public async Task RecordLlmUsageAsync(Guid userId, int transactionCount, CancellationToken ct = default)
    {
        var usage = await GetOrCreateCurrentMonthUsageAsync(userId, ct);
        usage.LlmCategorizationCount += transactionCount;
        usage.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Recorded LLM usage for user {UserId}: +{Count} transactions (total this month: {Total})",
            userId, transactionCount, usage.LlmCategorizationCount);
    }

    public async Task RecordRuleSuggestionUsageAsync(Guid userId, CancellationToken ct = default)
    {
        var usage = await GetOrCreateCurrentMonthUsageAsync(userId, ct);
        usage.RuleSuggestionCount += 1;
        usage.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Recorded rule suggestion usage for user {UserId} (total this month: {Total})",
            userId, usage.RuleSuggestionCount);
    }

    /// <summary>
    /// Fetches the user's subscription with plan data, cached for the request scope.
    /// </summary>
    private async Task<UserSubscription?> GetUserSubscriptionAsync(Guid userId, CancellationToken ct)
    {
        if (_cachedUserId == userId)
            return _cachedSubscription;

        _cachedSubscription = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        _cachedUserId = userId;

        return _cachedSubscription;
    }

    /// <summary>
    /// Derives the subscription tier from subscription data without a DB call.
    /// </summary>
    private static SubscriptionTier ResolveTier(UserSubscription? subscription)
    {
        if (subscription == null || subscription.Status == "free")
            return SubscriptionTier.Free;

        if (subscription.Status != "active")
            return SubscriptionTier.Free;

        var planName = subscription.Plan?.Name?.ToLowerInvariant() ?? "";
        if (planName.Contains("family"))
            return SubscriptionTier.Family;

        return SubscriptionTier.Pro;
    }

    /// <summary>
    /// Resolves the LLM quota from a subscription's plan without a DB call.
    /// </summary>
    private static int ResolvePlanLlmQuota(UserSubscription? subscription)
    {
        if (subscription?.Plan?.MaxAiCallsPerMonth > 0)
            return subscription.Plan.MaxAiCallsPerMonth;

        return ProLlmQuotaPerMonth;
    }

    private async Task<(int LlmCategorizationCount, int RuleSuggestionCount)> GetCurrentMonthUsageAsync(
        Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var usage = await _dbContext.AiCategorizationUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month, ct);

        return usage != null
            ? (usage.LlmCategorizationCount, usage.RuleSuggestionCount)
            : (0, 0);
    }

    /// <summary>
    /// Finds or creates the usage record for the current month.
    /// Uses IgnoreQueryFilters to handle soft-deleted rows (revives them if found).
    /// Handles concurrent creation via try-catch on unique constraint violation.
    /// </summary>
    private async Task<AiCategorizationUsage> GetOrCreateCurrentMonthUsageAsync(
        Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Bypass soft-delete filter so we find and revive soft-deleted rows
        // instead of hitting a unique constraint violation.
        var usage = await _dbContext.AiCategorizationUsages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month, ct);

        if (usage != null)
        {
            if (usage.IsDeleted)
            {
                usage.IsDeleted = false;
                usage.DeletedAt = null;
                usage.LlmCategorizationCount = 0;
                usage.RuleSuggestionCount = 0;
            }
            return usage;
        }

        usage = new AiCategorizationUsage
        {
            UserId = userId,
            Year = now.Year,
            Month = now.Month,
            LlmCategorizationCount = 0,
            RuleSuggestionCount = 0
        };

        _dbContext.AiCategorizationUsages.Add(usage);

        try
        {
            // Save immediately to detect unique constraint violations from concurrent requests.
            // This path only executes once per user per month when the record is first created.
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race condition: another request created the record concurrently.
            // Detach the failed entity and re-fetch the winner's record.
            _dbContext.Entry(usage).State = EntityState.Detached;
            usage = await _dbContext.AiCategorizationUsages
                .IgnoreQueryFilters()
                .FirstAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month, ct);
        }

        return usage;
    }
}
