using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
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

        var subscription = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

        if (subscription == null || subscription.Status == "free")
            return SubscriptionTier.Free;

        if (subscription.Status != "active")
            return SubscriptionTier.Free;

        // Derive tier from plan name (case-insensitive)
        var planName = subscription.Plan?.Name?.ToLowerInvariant() ?? "";
        if (planName.Contains("family"))
            return SubscriptionTier.Family;

        // Any active paid subscription = Pro
        return SubscriptionTier.Pro;
    }

    public async Task<bool> CanUseLlmCategorizationAsync(Guid userId, CancellationToken ct = default)
    {
        var tier = await GetUserTierAsync(userId, ct);
        return tier switch
        {
            SubscriptionTier.SelfHosted => true,
            SubscriptionTier.Pro or SubscriptionTier.Family => await GetRemainingLlmQuotaAsync(userId, ct) > 0,
            _ => false
        };
    }

    public async Task<bool> CanUseAiRuleSuggestionsAsync(Guid userId, CancellationToken ct = default)
    {
        var tier = await GetUserTierAsync(userId, ct);
        return tier switch
        {
            SubscriptionTier.SelfHosted => true,
            SubscriptionTier.Pro or SubscriptionTier.Family => await GetRemainingRuleSuggestionQuotaAsync(userId, ct) > 0,
            _ => false
        };
    }

    public async Task<int> GetRemainingLlmQuotaAsync(Guid userId, CancellationToken ct = default)
    {
        var tier = await GetUserTierAsync(userId, ct);
        if (tier == SubscriptionTier.SelfHosted)
            return int.MaxValue;
        if (tier == SubscriptionTier.Free)
            return 0;

        var quota = await GetPlanLlmQuotaAsync(userId, ct);
        var usage = await GetCurrentMonthUsageAsync(userId, ct);
        return Math.Max(0, quota - usage.LlmCategorizationCount);
    }

    public async Task<int> GetRemainingRuleSuggestionQuotaAsync(Guid userId, CancellationToken ct = default)
    {
        var tier = await GetUserTierAsync(userId, ct);
        if (tier == SubscriptionTier.SelfHosted)
            return int.MaxValue;
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
    /// Gets the LLM quota from the user's billing plan, falling back to ProLlmQuotaPerMonth.
    /// </summary>
    private async Task<int> GetPlanLlmQuotaAsync(Guid userId, CancellationToken ct)
    {
        var subscription = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);

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

    private async Task<Domain.Entities.AiCategorizationUsage> GetOrCreateCurrentMonthUsageAsync(
        Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var usage = await _dbContext.AiCategorizationUsages
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month, ct);

        if (usage != null)
            return usage;

        usage = new Domain.Entities.AiCategorizationUsage
        {
            UserId = userId,
            Year = now.Year,
            Month = now.Month,
            LlmCategorizationCount = 0,
            RuleSuggestionCount = 0
        };

        _dbContext.AiCategorizationUsages.Add(usage);
        await _dbContext.SaveChangesAsync(ct);
        return usage;
    }
}
