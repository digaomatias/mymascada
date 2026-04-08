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
            return new AiFeatureAccessResult(true, SubscriptionTier.SelfHosted);

        var subscription = await GetUserSubscriptionAsync(userId, ct);
        var tier = ResolveTier(subscription);

        if (tier == SubscriptionTier.Free)
            return new AiFeatureAccessResult(false, tier,
                "LLM categorization is not available on the Free plan. Upgrade to Pro for AI-powered categorization.");

        var quota = ResolvePlanLlmQuota(subscription);
        var usage = await GetCurrentMonthUsageAsync(userId, ct);
        var remaining = Math.Max(0, quota - usage.LlmCategorizationCount);

        if (remaining <= 0)
            return new AiFeatureAccessResult(false, tier,
                "Monthly LLM categorization quota exceeded. Quota resets at the start of next month.");

        return new AiFeatureAccessResult(true, tier);
    }

    public async Task<AiFeatureAccessResult> CanUseAiRuleSuggestionsAsync(Guid userId, CancellationToken ct = default)
    {
        if (!_featureFlags.StripeBilling)
            return new AiFeatureAccessResult(true, SubscriptionTier.SelfHosted);

        var subscription = await GetUserSubscriptionAsync(userId, ct);
        var tier = ResolveTier(subscription);

        if (tier == SubscriptionTier.Free)
            return new AiFeatureAccessResult(false, tier,
                "AI-enhanced rule suggestions are not available on the Free plan. Basic rule suggestions are generated automatically.");

        var usage = await GetCurrentMonthUsageAsync(userId, ct);
        var remaining = Math.Max(0, ProRuleSuggestionQuotaPerMonth - usage.RuleSuggestionCount);

        if (remaining <= 0)
            return new AiFeatureAccessResult(false, tier,
                "Monthly AI rule suggestion quota exceeded. Quota resets at the start of next month.");

        return new AiFeatureAccessResult(true, tier);
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
    /// Fetches the user's subscription with plan data (single DB round-trip).
    /// </summary>
    private Task<UserSubscription?> GetUserSubscriptionAsync(Guid userId, CancellationToken ct)
    {
        return _dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
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

    private async Task<AiCategorizationUsage> GetOrCreateCurrentMonthUsageAsync(
        Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var usage = await _dbContext.AiCategorizationUsages
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month, ct);

        if (usage != null)
            return usage;

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
            // This is the only path that creates new records (once per user per month).
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race condition: another request created the record concurrently.
            // Detach the failed entity and re-fetch the winner's record.
            _dbContext.Entry(usage).State = EntityState.Detached;
            usage = await _dbContext.AiCategorizationUsages
                .FirstAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month, ct);
        }

        return usage;
    }
}
