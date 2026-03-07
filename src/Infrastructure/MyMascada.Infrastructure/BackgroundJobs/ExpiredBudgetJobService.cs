using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.Commands;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-based background job that processes expired budget periods daily.
/// For recurring budgets, creates new periods (with rollover where applicable).
/// For non-recurring budgets, marks them as completed.
/// </summary>
public class ExpiredBudgetJobService : IExpiredBudgetJobService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ExpiredBudgetJobService> _logger;

    public ExpiredBudgetJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ExpiredBudgetJobService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Processes all users' expired budgets.
    /// Scheduled to run daily at 1:00 AM.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ProcessAllUsersAsync(object? performContext = null)
    {
        var startTime = DateTime.UtcNow;
        var totalUsersProcessed = 0;
        var totalBudgetsProcessed = 0;
        var totalNewPeriodsCreated = 0;
        var failedUsers = new List<Guid>();

        _logger.LogInformation("Starting daily expired budget processing job at {StartTime}", startTime);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var budgetRepository = scope.ServiceProvider.GetRequiredService<IBudgetRepository>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Find all users with expired active budgets
            var userIds = await budgetRepository.GetUserIdsWithExpiredActiveBudgetsAsync();
            var userIdList = userIds.ToList();

            _logger.LogInformation("Found {UserCount} users with expired budgets to process", userIdList.Count);

            foreach (var userId in userIdList)
            {
                try
                {
                    _logger.LogDebug("Processing expired budgets for user {UserId}...", userId);

                    var result = await mediator.Send(new ProcessExpiredBudgetsCommand
                    {
                        UserId = userId,
                        PreviewOnly = false
                    });

                    totalBudgetsProcessed += result.TotalBudgetsProcessed;
                    totalNewPeriodsCreated += result.NewBudgetsCreated;
                    totalUsersProcessed++;

                    _logger.LogDebug("User {UserId}: processed {BudgetCount} budget(s), created {NewCount} new period(s)",
                        userId, result.TotalBudgetsProcessed, result.NewBudgetsCreated);
                }
                catch (Exception ex)
                {
                    failedUsers.Add(userId);
                    _logger.LogError(ex, "Failed to process expired budgets for user {UserId}", userId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in expired budget processing job");
            throw;
        }

        var duration = DateTime.UtcNow - startTime;

        if (failedUsers.Any())
        {
            _logger.LogWarning(
                "Expired budget job completed with errors in {Duration}ms. " +
                "Users: {Processed}/{Total}, Budgets: {BudgetsProcessed}, New periods: {NewPeriods}, Failed users: {FailedCount}",
                duration.TotalMilliseconds, totalUsersProcessed, totalUsersProcessed + failedUsers.Count,
                totalBudgetsProcessed, totalNewPeriodsCreated, failedUsers.Count);
        }
        else
        {
            _logger.LogInformation(
                "Expired budget job completed successfully in {Duration}ms. " +
                "Users: {Processed}, Budgets: {BudgetsProcessed}, New periods: {NewPeriods}",
                duration.TotalMilliseconds, totalUsersProcessed,
                totalBudgetsProcessed, totalNewPeriodsCreated);
        }
    }
}
