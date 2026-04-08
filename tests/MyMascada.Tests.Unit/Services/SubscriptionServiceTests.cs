using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Services.Subscription;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Services;

public class SubscriptionServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IFeatureFlags _featureFlags;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly Guid _userId = Guid.NewGuid();

    public SubscriptionServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"SubscriptionServiceTests_{Guid.NewGuid()}")
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _featureFlags = Substitute.For<IFeatureFlags>();
        _logger = Substitute.For<ILogger<SubscriptionService>>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private SubscriptionService CreateService() =>
        new(_dbContext, _featureFlags, _logger);

    // --- Self-hosted detection ---

    [Fact]
    public async Task IsSelfHostedAsync_StripeBillingDisabled_ReturnsTrue()
    {
        _featureFlags.StripeBilling.Returns(false);
        var service = CreateService();

        var result = await service.IsSelfHostedAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSelfHostedAsync_StripeBillingEnabled_ReturnsFalse()
    {
        _featureFlags.StripeBilling.Returns(true);
        var service = CreateService();

        var result = await service.IsSelfHostedAsync();

        result.Should().BeFalse();
    }

    // --- GetUserTierAsync ---

    [Fact]
    public async Task GetUserTierAsync_SelfHosted_ReturnsSelfHosted()
    {
        _featureFlags.StripeBilling.Returns(false);
        var service = CreateService();

        var tier = await service.GetUserTierAsync(_userId);

        tier.Should().Be(SubscriptionTier.SelfHosted);
    }

    [Fact]
    public async Task GetUserTierAsync_NoSubscription_ReturnsFree()
    {
        _featureFlags.StripeBilling.Returns(true);
        var service = CreateService();

        var tier = await service.GetUserTierAsync(_userId);

        tier.Should().Be(SubscriptionTier.Free);
    }

    [Fact]
    public async Task GetUserTierAsync_FreeStatusSubscription_ReturnsFree()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "free", "Pro Plan");
        var service = CreateService();

        var tier = await service.GetUserTierAsync(_userId);

        tier.Should().Be(SubscriptionTier.Free);
    }

    [Fact]
    public async Task GetUserTierAsync_ActiveProSubscription_ReturnsPro()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Pro Plan");
        var service = CreateService();

        var tier = await service.GetUserTierAsync(_userId);

        tier.Should().Be(SubscriptionTier.Pro);
    }

    [Fact]
    public async Task GetUserTierAsync_ActiveFamilySubscription_ReturnsFamily()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Family Plan");
        var service = CreateService();

        var tier = await service.GetUserTierAsync(_userId);

        tier.Should().Be(SubscriptionTier.Family);
    }

    [Fact]
    public async Task GetUserTierAsync_CancelledSubscription_ReturnsFree()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "cancelled", "Pro Plan");
        var service = CreateService();

        var tier = await service.GetUserTierAsync(_userId);

        tier.Should().Be(SubscriptionTier.Free);
    }

    [Fact]
    public async Task GetUserTierAsync_PastDueSubscription_ReturnsFree()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "past_due", "Pro Plan");
        var service = CreateService();

        var tier = await service.GetUserTierAsync(_userId);

        tier.Should().Be(SubscriptionTier.Free);
    }

    // --- Quota checks ---

    [Fact]
    public async Task CanUseLlmCategorizationAsync_SelfHosted_ReturnsAllowed()
    {
        _featureFlags.StripeBilling.Returns(false);
        var service = CreateService();

        var result = await service.CanUseLlmCategorizationAsync(_userId);

        result.IsAllowed.Should().BeTrue();
        result.Tier.Should().Be(SubscriptionTier.SelfHosted);
    }

    [Fact]
    public async Task CanUseLlmCategorizationAsync_FreeUser_ReturnsDeniedWithReason()
    {
        _featureFlags.StripeBilling.Returns(true);
        var service = CreateService();

        var result = await service.CanUseLlmCategorizationAsync(_userId);

        result.IsAllowed.Should().BeFalse();
        result.Tier.Should().Be(SubscriptionTier.Free);
        result.DenialReason.Should().Contain("Free plan");
    }

    [Fact]
    public async Task CanUseLlmCategorizationAsync_ProUserWithQuota_ReturnsAllowed()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Pro Plan", maxAiCalls: 200);
        var service = CreateService();

        var result = await service.CanUseLlmCategorizationAsync(_userId);

        result.IsAllowed.Should().BeTrue();
        result.Tier.Should().Be(SubscriptionTier.Pro);
    }

    [Fact]
    public async Task CanUseLlmCategorizationAsync_ProUserQuotaExhausted_ReturnsDeniedWithReason()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Pro Plan", maxAiCalls: 10);
        await SeedUsage(_userId, llmCount: 10);
        var service = CreateService();

        var result = await service.CanUseLlmCategorizationAsync(_userId);

        result.IsAllowed.Should().BeFalse();
        result.Tier.Should().Be(SubscriptionTier.Pro);
        result.DenialReason.Should().Contain("quota exceeded");
    }

    [Fact]
    public async Task CanUseAiRuleSuggestionsAsync_FreeUser_ReturnsDeniedWithReason()
    {
        _featureFlags.StripeBilling.Returns(true);
        var service = CreateService();

        var result = await service.CanUseAiRuleSuggestionsAsync(_userId);

        result.IsAllowed.Should().BeFalse();
        result.Tier.Should().Be(SubscriptionTier.Free);
        result.DenialReason.Should().Contain("Free plan");
    }

    [Fact]
    public async Task CanUseAiRuleSuggestionsAsync_ProUserWithQuota_ReturnsAllowed()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Pro Plan");
        var service = CreateService();

        var result = await service.CanUseAiRuleSuggestionsAsync(_userId);

        result.IsAllowed.Should().BeTrue();
        result.Tier.Should().Be(SubscriptionTier.Pro);
    }

    [Fact]
    public async Task CanUseAiRuleSuggestionsAsync_ProUserQuotaExhausted_ReturnsDeniedWithReason()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Pro Plan");
        await SeedUsage(_userId, ruleSuggestionCount: 5); // 5/month quota
        var service = CreateService();

        var result = await service.CanUseAiRuleSuggestionsAsync(_userId);

        result.IsAllowed.Should().BeFalse();
        result.Tier.Should().Be(SubscriptionTier.Pro);
        result.DenialReason.Should().Contain("quota exceeded");
    }

    [Fact]
    public async Task CanUseLlmCategorizationAsync_FamilyUserWithQuota_ReturnsAllowed()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Family Plan", maxAiCalls: 200);
        var service = CreateService();

        var result = await service.CanUseLlmCategorizationAsync(_userId);

        result.IsAllowed.Should().BeTrue();
        result.Tier.Should().Be(SubscriptionTier.Family);
    }

    [Fact]
    public async Task CanUseLlmCategorizationAsync_FamilyUserQuotaExhausted_ReturnsDenied()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Family Plan", maxAiCalls: 10);
        await SeedUsage(_userId, llmCount: 10);
        var service = CreateService();

        var result = await service.CanUseLlmCategorizationAsync(_userId);

        result.IsAllowed.Should().BeFalse();
        result.Tier.Should().Be(SubscriptionTier.Family);
        result.DenialReason.Should().Contain("quota exceeded");
    }

    [Fact]
    public async Task CanUseAiRuleSuggestionsAsync_SelfHosted_ReturnsAllowed()
    {
        _featureFlags.StripeBilling.Returns(false);
        var service = CreateService();

        var result = await service.CanUseAiRuleSuggestionsAsync(_userId);

        result.IsAllowed.Should().BeTrue();
        result.Tier.Should().Be(SubscriptionTier.SelfHosted);
    }

    [Fact]
    public async Task CanUseAiRuleSuggestionsAsync_FamilyUserWithQuota_ReturnsAllowed()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Family Plan");
        var service = CreateService();

        var result = await service.CanUseAiRuleSuggestionsAsync(_userId);

        result.IsAllowed.Should().BeTrue();
        result.Tier.Should().Be(SubscriptionTier.Family);
    }

    [Fact]
    public async Task CanUseAiRuleSuggestionsAsync_FamilyUserQuotaExhausted_ReturnsDenied()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Family Plan");
        await SeedUsage(_userId, ruleSuggestionCount: 5);
        var service = CreateService();

        var result = await service.CanUseAiRuleSuggestionsAsync(_userId);

        result.IsAllowed.Should().BeFalse();
        result.Tier.Should().Be(SubscriptionTier.Family);
        result.DenialReason.Should().Contain("quota exceeded");
    }

    // --- Remaining quota ---

    [Fact]
    public async Task GetRemainingLlmQuotaAsync_SelfHosted_ReturnsMaxValue()
    {
        _featureFlags.StripeBilling.Returns(false);
        var service = CreateService();

        var remaining = await service.GetRemainingLlmQuotaAsync(_userId);

        remaining.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task GetRemainingLlmQuotaAsync_FreeUser_ReturnsZero()
    {
        _featureFlags.StripeBilling.Returns(true);
        var service = CreateService();

        var remaining = await service.GetRemainingLlmQuotaAsync(_userId);

        remaining.Should().Be(0);
    }

    [Fact]
    public async Task GetRemainingLlmQuotaAsync_ProUserPartiallyUsed_ReturnsCorrectRemaining()
    {
        _featureFlags.StripeBilling.Returns(true);
        await SeedSubscription(_userId, "active", "Pro Plan", maxAiCalls: 200);
        await SeedUsage(_userId, llmCount: 75);
        var service = CreateService();

        var remaining = await service.GetRemainingLlmQuotaAsync(_userId);

        remaining.Should().Be(125);
    }

    // --- Usage recording ---
    // RecordLlmUsageAsync and RecordRuleSuggestionUsageAsync now use atomic PostgreSQL
    // upserts (INSERT ... ON CONFLICT DO UPDATE) which require a real relational provider.
    // These should be covered by integration tests against PostgreSQL.

    // --- Helpers ---

    private async Task SeedSubscription(Guid userId, string status, string planName, int maxAiCalls = 200)
    {
        var plan = new BillingPlan
        {
            Name = planName,
            StripePriceId = $"price_{Guid.NewGuid():N}",
            MaxAccounts = 10,
            MaxTransactionsPerMonth = 5000,
            MaxAiCallsPerMonth = maxAiCalls,
            IsActive = true
        };
        _dbContext.BillingPlans.Add(plan);
        await _dbContext.SaveChangesAsync();

        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanId = plan.Id,
            Status = status
        };
        _dbContext.UserSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedUsage(Guid userId, int llmCount = 0, int ruleSuggestionCount = 0)
    {
        var now = DateTime.UtcNow;
        var usage = new AiCategorizationUsage
        {
            UserId = userId,
            Year = now.Year,
            Month = now.Month,
            LlmCategorizationCount = llmCount,
            RuleSuggestionCount = ruleSuggestionCount
        };
        _dbContext.AiCategorizationUsages.Add(usage);
        await _dbContext.SaveChangesAsync();
    }
}
