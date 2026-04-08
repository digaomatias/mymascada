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
/// groups by normalized description, and populates CategorizationHistory.
/// Idempotent — safe to re-run (uses absolute counts instead of incremental).
/// </summary>
public class CategorizationHistoryBackfillJobService : ICategorizationHistoryBackfillJobService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<CategorizationHistoryBackfillJobService> _logger;

    /// <summary>
    /// Maximum categorized transactions to process per user during backfill.
    /// Set high enough to cover full history for most users.
    /// </summary>
    private const int MaxTransactionsPerUser = 10_000;

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

        // Use a separate scope to fetch user IDs, then dispose before per-user processing
        IReadOnlyList<Guid> userIds;
        using (var listScope = _serviceScopeFactory.CreateScope())
        {
            var historyRepo = listScope.ServiceProvider.GetRequiredService<ICategorizationHistoryRepository>();
            userIds = await historyRepo.GetDistinctUserIdsWithCategorizedTransactionsAsync(ct);
        }

        _logger.LogInformation("Found {UserCount} users with categorized transactions to backfill", userIds.Count);

        foreach (var userId in userIds)
        {
            ct.ThrowIfCancellationRequested();

            // Fresh scope per user — prevents DbContext entity accumulation and isolates failures
            using var scope = _serviceScopeFactory.CreateScope();
            var historyRepo = scope.ServiceProvider.GetRequiredService<ICategorizationHistoryRepository>();
            var transactionRepo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

            try
            {
                var transactions = (await transactionRepo.GetCategorizedTransactionsAsync(userId, MaxTransactionsPerUser, ct)).ToList();

                // Group by normalized description only. For descriptions that were
                // re-categorized over time, pick the newest category (transactions
                // arrive in descending CreatedAt order, so First() is the newest).
                // Count only includes transactions matching the winning category to
                // avoid inflating confidence for re-categorized descriptions.
                var groups = transactions
                    .Where(t => t.CategoryId.HasValue)
                    .Select(t => new
                    {
                        Normalized = DescriptionNormalizer.Normalize(t.Description),
                        t.Description,
                        CategoryId = t.CategoryId!.Value
                    })
                    .Where(g => !string.IsNullOrWhiteSpace(g.Normalized))
                    .GroupBy(g => g.Normalized)
                    .Select(g =>
                    {
                        var newest = g.First(); // descending CreatedAt → first is newest
                        return new
                        {
                            Normalized = g.Key,
                            newest.Description,
                            newest.CategoryId,
                            Count = g.Count(x => x.CategoryId == newest.CategoryId)
                        };
                    })
                    .ToList();

                foreach (var group in groups)
                {
                    await historyRepo.UpsertWithAbsoluteCountAsync(
                        userId,
                        group.Normalized,
                        group.Description,
                        group.CategoryId,
                        group.Count,
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
            catch (OperationCanceledException)
            {
                throw;
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
