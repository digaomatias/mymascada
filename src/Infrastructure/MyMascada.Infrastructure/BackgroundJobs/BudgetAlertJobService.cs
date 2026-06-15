using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily Hangfire job that checks every user's active budgets and fires
/// budget-threshold / budget-exceeded notifications via
/// <see cref="INotificationTriggerService.CheckBudgetThresholdsAsync"/>.
/// The trigger service handles idempotency, so re-running this job never
/// produces duplicate alerts for the same category in the same period.
/// </summary>
public class BudgetAlertJobService : IBudgetAlertJobService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<BudgetAlertJobService> _logger;

    public BudgetAlertJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<BudgetAlertJobService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ProcessAllUsersAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting daily budget alert job");
        var startTime = DateTime.UtcNow;
        var processed = 0;
        var errors = new List<Exception>();

        IReadOnlyList<Guid> userIds;
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var budgetRepository = scope.ServiceProvider.GetRequiredService<IBudgetRepository>();
            userIds = (await budgetRepository.GetUserIdsWithActiveBudgetsAsync(ct)).ToList();
        }

        _logger.LogInformation("Found {UserCount} users with active budgets", userIds.Count);

        foreach (var userId in userIds)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Fresh scope per user keeps DbContext memory bounded to one user
                // at a time, mirroring the other per-user recurring jobs.
                using var scope = _serviceScopeFactory.CreateScope();
                var triggerService = scope.ServiceProvider.GetRequiredService<INotificationTriggerService>();
                await triggerService.CheckBudgetThresholdsAsync(userId, ct);
                processed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One user's failure must not abort the rest of the run.
                _logger.LogError(ex, "Error checking budget thresholds for user {UserId}", userId);
                errors.Add(ex);
            }
        }

        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Daily budget alert job completed in {Elapsed}. Processed {Processed}/{Total} users, errors: {ErrorCount}",
            elapsed, processed, userIds.Count, errors.Count);

        if (errors.Count > 0)
        {
            throw new AggregateException(
                $"Budget alert check failed for {errors.Count}/{userIds.Count} users", errors);
        }
    }
}
