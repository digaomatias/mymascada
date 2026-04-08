using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Services;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Weekly Hangfire job that checks trigger conditions per user and generates
/// rule suggestions when thresholds are met. Sends a notification when new
/// suggestions are available.
/// </summary>
public class RuleSuggestionGenerationJobService : IRuleSuggestionGenerationJobService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RuleSuggestionGenerationJobService> _logger;

    public RuleSuggestionGenerationJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RuleSuggestionGenerationJobService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 300 })]
    public async Task ProcessAllUsersAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting weekly rule suggestion generation job");
        var startTime = DateTime.UtcNow;
        var usersProcessed = 0;
        var totalSuggestions = 0;

        IReadOnlyList<Guid> userIds;

        // Get all users with categorization history (same approach as backfill job)
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var historyRepo = scope.ServiceProvider.GetRequiredService<ICategorizationHistoryRepository>();
            userIds = await historyRepo.GetDistinctUserIdsWithCategorizedTransactionsAsync(ct);
        }

        _logger.LogInformation("Found {UserCount} users with categorization history", userIds.Count);

        foreach (var userId in userIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var suggestionsGenerated = await ProcessUserAsync(userId, ct);
                if (suggestionsGenerated > 0)
                {
                    totalSuggestions += suggestionsGenerated;
                    usersProcessed++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing rule suggestions for user {UserId}", userId);
            }
        }

        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Weekly rule suggestion job completed in {Elapsed}. " +
            "Users processed: {UsersProcessed}/{TotalUsers}, suggestions generated: {TotalSuggestions}",
            elapsed, usersProcessed, userIds.Count, totalSuggestions);
    }

    private async Task<int> ProcessUserAsync(Guid userId, CancellationToken ct)
    {
        // Fresh scope per user to prevent DbContext accumulation
        using var scope = _serviceScopeFactory.CreateScope();
        var suggestionService = scope.ServiceProvider.GetRequiredService<IRuleSuggestionService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationTriggerService>();

        // Check trigger conditions first
        var shouldGenerate = await suggestionService.ShouldGenerateRuleSuggestionsAsync(userId, ct);
        if (!shouldGenerate)
            return 0;

        // Generate suggestions
        var suggestions = await suggestionService.GenerateSuggestionsAsync(userId);

        if (suggestions.Count > 0)
        {
            _logger.LogInformation(
                "Generated {Count} rule suggestions for user {UserId}",
                suggestions.Count, userId);

            // Send notification
            await notificationService.NotifyRuleSuggestionsAvailableAsync(userId, suggestions.Count, ct);
        }

        return suggestions.Count;
    }
}
