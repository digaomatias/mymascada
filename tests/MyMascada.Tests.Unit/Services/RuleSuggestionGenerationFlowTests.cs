using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.BackgroundJobs;

namespace MyMascada.Tests.Unit.Services;

/// <summary>
/// Integration-style test verifying the full rule suggestion generation flow:
/// History populated → trigger met → suggestions generated → notification sent.
/// Uses mocked services to verify the orchestration logic.
/// </summary>
public class RuleSuggestionGenerationFlowTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task ProcessAllUsers_TriggerMet_GeneratesSuggestionsAndNotifies()
    {
        // Arrange
        var historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        var ruleSuggestionService = Substitute.For<IRuleSuggestionService>();
        var notificationService = Substitute.For<INotificationTriggerService>();

        // User has categorization history
        historyRepo.GetDistinctUserIdsWithCategorizedTransactionsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { _userId });

        // Trigger conditions met
        ruleSuggestionService.ShouldGenerateRuleSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Suggestions generated
        var generatedSuggestions = new List<RuleSuggestion>
        {
            new() { Name = "PAK N SAVE Rule", Pattern = "pak n save", SuggestedCategoryId = 1, ConfidenceScore = 0.85 },
            new() { Name = "UBER Rule", Pattern = "uber", SuggestedCategoryId = 3, ConfidenceScore = 0.90 }
        };
        ruleSuggestionService.GenerateSuggestionsAsync(_userId, Arg.Any<int>(), Arg.Any<double>())
            .Returns(generatedSuggestions);

        // Build service provider for scoped resolution
        var services = new ServiceCollection();
        services.AddScoped(_ => historyRepo);
        services.AddScoped(_ => ruleSuggestionService);
        services.AddScoped(_ => notificationService);
        var serviceProvider = services.BuildServiceProvider();

        var job = new RuleSuggestionGenerationJobService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<RuleSuggestionGenerationJobService>>());

        // Act
        await job.ProcessAllUsersAsync();

        // Assert: trigger conditions were checked
        await ruleSuggestionService.Received(1)
            .ShouldGenerateRuleSuggestionsAsync(_userId, Arg.Any<CancellationToken>());

        // Assert: suggestions were generated
        await ruleSuggestionService.Received(1)
            .GenerateSuggestionsAsync(_userId, Arg.Any<int>(), Arg.Any<double>());

        // Assert: notification was sent with correct count
        await notificationService.Received(1)
            .NotifyRuleSuggestionsAvailableAsync(_userId, 2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAllUsers_TriggerNotMet_DoesNotGenerate()
    {
        var historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        var ruleSuggestionService = Substitute.For<IRuleSuggestionService>();
        var notificationService = Substitute.For<INotificationTriggerService>();

        historyRepo.GetDistinctUserIdsWithCategorizedTransactionsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { _userId });

        // Trigger conditions NOT met
        ruleSuggestionService.ShouldGenerateRuleSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(false);

        var services = new ServiceCollection();
        services.AddScoped(_ => historyRepo);
        services.AddScoped(_ => ruleSuggestionService);
        services.AddScoped(_ => notificationService);
        var serviceProvider = services.BuildServiceProvider();

        var job = new RuleSuggestionGenerationJobService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<RuleSuggestionGenerationJobService>>());

        await job.ProcessAllUsersAsync();

        // Should check trigger but NOT generate
        await ruleSuggestionService.Received(1)
            .ShouldGenerateRuleSuggestionsAsync(_userId, Arg.Any<CancellationToken>());
        await ruleSuggestionService.DidNotReceive()
            .GenerateSuggestionsAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<double>());
        await notificationService.DidNotReceive()
            .NotifyRuleSuggestionsAvailableAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAllUsers_NoSuggestionsGenerated_DoesNotNotify()
    {
        var historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        var ruleSuggestionService = Substitute.For<IRuleSuggestionService>();
        var notificationService = Substitute.For<INotificationTriggerService>();

        historyRepo.GetDistinctUserIdsWithCategorizedTransactionsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { _userId });

        ruleSuggestionService.ShouldGenerateRuleSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(true);
        ruleSuggestionService.GenerateSuggestionsAsync(_userId, Arg.Any<int>(), Arg.Any<double>())
            .Returns(new List<RuleSuggestion>()); // Empty list

        var services = new ServiceCollection();
        services.AddScoped(_ => historyRepo);
        services.AddScoped(_ => ruleSuggestionService);
        services.AddScoped(_ => notificationService);
        var serviceProvider = services.BuildServiceProvider();

        var job = new RuleSuggestionGenerationJobService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<RuleSuggestionGenerationJobService>>());

        await job.ProcessAllUsersAsync();

        // Generated but got 0 suggestions — should NOT notify
        await notificationService.DidNotReceive()
            .NotifyRuleSuggestionsAvailableAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAllUsers_OneUserFails_ContinuesWithOthers()
    {
        var userId2 = Guid.NewGuid();

        var historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        var ruleSuggestionService = Substitute.For<IRuleSuggestionService>();
        var notificationService = Substitute.For<INotificationTriggerService>();

        historyRepo.GetDistinctUserIdsWithCategorizedTransactionsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { _userId, userId2 });

        // First user throws
        ruleSuggestionService.ShouldGenerateRuleSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns<bool>(x => throw new InvalidOperationException("DB error"));

        // Second user succeeds
        ruleSuggestionService.ShouldGenerateRuleSuggestionsAsync(userId2, Arg.Any<CancellationToken>())
            .Returns(true);
        ruleSuggestionService.GenerateSuggestionsAsync(userId2, Arg.Any<int>(), Arg.Any<double>())
            .Returns(new List<RuleSuggestion> { new() { Name = "Test", Pattern = "test", SuggestedCategoryId = 1 } });

        var services = new ServiceCollection();
        services.AddScoped(_ => historyRepo);
        services.AddScoped(_ => ruleSuggestionService);
        services.AddScoped(_ => notificationService);
        var serviceProvider = services.BuildServiceProvider();

        var job = new RuleSuggestionGenerationJobService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<ILogger<RuleSuggestionGenerationJobService>>());

        // Should not throw — errors are caught per-user
        var act = () => job.ProcessAllUsersAsync();
        await act.Should().NotThrowAsync();

        // Second user still processed
        await ruleSuggestionService.Received(1)
            .ShouldGenerateRuleSuggestionsAsync(userId2, Arg.Any<CancellationToken>());
        await notificationService.Received(1)
            .NotifyRuleSuggestionsAvailableAsync(userId2, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShouldGenerateRuleSuggestions_EnoughHistoryNoRules_ReturnsTrue()
    {
        // Direct test of the trigger condition in RuleSuggestionService
        var historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        var ruleRepo = Substitute.For<ICategorizationRuleRepository>();
        var transactionRepo = Substitute.For<ITransactionRepository>();
        var categoryRepo = Substitute.For<ICategoryRepository>();
        var analyzerFactory = Substitute.For<IRuleSuggestionAnalyzerFactory>();
        var featureFlags = Substitute.For<IFeatureFlags>();

        // 10+ history entries
        var entries = Enumerable.Range(1, 12)
            .Select(i => new CategorizationHistory
            {
                UserId = _userId,
                NormalizedDescription = $"store {i}",
                OriginalDescription = $"STORE {i}",
                CategoryId = 1,
                MatchCount = 1,
                Source = CategorizationHistorySource.Manual
            })
            .ToList();

        historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CategorizationHistory>)entries);
        ruleRepo.GetActiveRulesForUserAsync(_userId)
            .Returns(Array.Empty<CategorizationRule>());

        var service = new RuleSuggestionService(
            Substitute.For<IRuleSuggestionRepository>(),
            transactionRepo,
            ruleRepo,
            categoryRepo,
            analyzerFactory,
            historyRepo,
            featureFlags);

        var result = await service.ShouldGenerateRuleSuggestionsAsync(_userId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ShouldGenerateRuleSuggestions_TooFewEntries_ReturnsFalse()
    {
        var historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        var ruleRepo = Substitute.For<ICategorizationRuleRepository>();

        // Only 5 entries (threshold is 10)
        var entries = Enumerable.Range(1, 5)
            .Select(i => new CategorizationHistory
            {
                UserId = _userId,
                NormalizedDescription = $"store {i}",
                CategoryId = 1
            })
            .ToList();

        historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CategorizationHistory>)entries);

        var service = new RuleSuggestionService(
            Substitute.For<IRuleSuggestionRepository>(),
            Substitute.For<ITransactionRepository>(),
            ruleRepo,
            Substitute.For<ICategoryRepository>(),
            Substitute.For<IRuleSuggestionAnalyzerFactory>(),
            historyRepo,
            Substitute.For<IFeatureFlags>());

        var result = await service.ShouldGenerateRuleSuggestionsAsync(_userId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldGenerateRuleSuggestions_AllCoveredByRules_ReturnsFalse()
    {
        var historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        var ruleRepo = Substitute.For<ICategorizationRuleRepository>();

        var entries = Enumerable.Range(1, 12)
            .Select(i => new CategorizationHistory
            {
                UserId = _userId,
                NormalizedDescription = $"netflix {i}",
                OriginalDescription = $"NETFLIX {i}",
                CategoryId = 2,
                MatchCount = 1,
                Source = CategorizationHistorySource.Manual
            })
            .ToList();

        var rules = new List<CategorizationRule>
        {
            new() { Pattern = "NETFLIX", Type = RuleType.Contains, IsCaseSensitive = false, IsActive = true, CategoryId = 2 }
        };

        historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<CategorizationHistory>)entries);
        ruleRepo.GetActiveRulesForUserAsync(_userId).Returns(rules);

        var service = new RuleSuggestionService(
            Substitute.For<IRuleSuggestionRepository>(),
            Substitute.For<ITransactionRepository>(),
            ruleRepo,
            Substitute.For<ICategoryRepository>(),
            Substitute.For<IRuleSuggestionAnalyzerFactory>(),
            historyRepo,
            Substitute.For<IFeatureFlags>());

        var result = await service.ShouldGenerateRuleSuggestionsAsync(_userId);

        result.Should().BeFalse();
    }
}
