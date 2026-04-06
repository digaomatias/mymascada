using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// One-time Hangfire job that scans all categorized transactions per user,
/// groups by normalized description + category, and populates CategorizationHistory.
/// Idempotent — safe to re-run.
/// </summary>
public class CategorizationHistoryBackfillJobService : ICategorizationHistoryBackfillJobService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CategorizationHistoryBackfillJobService> _logger;

    public CategorizationHistoryBackfillJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CategorizationHistoryBackfillJobService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
    public async Task BackfillAllUsersAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting categorization history backfill job");
        var startTime = DateTime.UtcNow;
        var totalEntries = 0;
        var totalUsers = 0;

        using var scope = _serviceScopeFactory.CreateScope();
        var historyRepo = scope.ServiceProvider.GetRequiredService<ICategorizationHistoryRepository>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var userIds = await historyRepo.GetDistinctUserIdsWithCategorizedTransactionsAsync(ct);
        _logger.LogInformation("Found {UserCount} users with categorized transactions to backfill", userIds.Count);

        foreach (var userId in userIds)
        {
            try
            {
                // Load categorized transactions (up to 2000 per user — covers typical history)
                var transactions = (await transactionRepo.GetCategorizedTransactionsAsync(userId, 2000, ct)).ToList();

                // Group by normalized description + category
                var groups = transactions
                    .Where(t => t.CategoryId.HasValue)
                    .Select(t => new
                    {
                        Normalized = DescriptionNormalizer.Normalize(t.Description),
                        t.Description,
                        CategoryId = t.CategoryId!.Value
                    })
                    .Where(g => !string.IsNullOrWhiteSpace(g.Normalized))
                    .GroupBy(g => new { g.Normalized, g.CategoryId })
                    .ToList();

                foreach (var group in groups)
                {
                    await historyRepo.UpsertAsync(
                        userId,
                        group.Key.Normalized,
                        group.First().Description,
                        group.Key.CategoryId,
                        CategorizationHistorySource.Backfill,
                        ct);
                    totalEntries++;
                }

                // Batch save per user instead of per entry
                await historyRepo.SaveChangesAsync(ct);

                totalUsers++;
                _logger.LogDebug(
                    "Backfilled {EntryCount} history entries for user {UserId}",
                    groups.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to backfill history for user {UserId}", userId);
            }
        }

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Categorization history backfill completed in {Duration}ms: " +
            "{TotalUsers} users, {TotalEntries} entries",
            duration.TotalMilliseconds, totalUsers, totalEntries);
    }
}
