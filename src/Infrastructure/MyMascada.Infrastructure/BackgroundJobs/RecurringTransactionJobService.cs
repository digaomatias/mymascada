using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Features.RecurringTransactions.Services;
using MyMascada.Domain.Common;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-based implementation of the user-created recurring transaction job.
/// Runs daily to auto-create due transactions or send reminder notifications.
/// Idempotent: occurrence uniqueness per (schedule, scheduled date) makes it
/// safe to run multiple times in a day.
/// </summary>
public class RecurringTransactionJobService : IRecurringTransactionJobService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RecurringTransactionJobService> _logger;

    public RecurringTransactionJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RecurringTransactionJobService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Processes all due recurring transactions across all users.
    /// Scheduled to run daily at 2:30 AM.
    /// Serialized via DisableConcurrentExecution so overlapping runs (retry +
    /// manual trigger) cannot race each other; combined with the Pending-claim
    /// occurrence rows this guarantees each scheduled date fires at most once.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [DisableConcurrentExecution(timeoutInSeconds: 30 * 60)]
    public async Task ProcessAllDueAsync(object? performContext = null)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("🔄 Starting daily recurring transaction job at {StartTime}", startTime);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var processingService = scope.ServiceProvider
                .GetRequiredService<IRecurringTransactionProcessingService>();

            var result = await processingService.ProcessDueAsync(DateTimeProvider.UtcNow.Date);

            var totalDuration = DateTime.UtcNow - startTime;

            _logger.LogInformation("✅ Daily recurring transaction job completed at {EndTime}. " +
                "📊 Stats: {SchedulesProcessed} schedules processed, {TransactionsCreated} transactions created, " +
                "{RemindersSent} reminders sent, {OccurrencesSkipped} occurrences skipped (already processed), " +
                "{SchedulesDeactivated} schedules deactivated, {SchedulesFailed} schedules failed. " +
                "⏱️ Duration: {Duration}s",
                DateTime.UtcNow,
                result.SchedulesProcessed,
                result.TransactionsCreated,
                result.RemindersSent,
                result.OccurrencesSkipped,
                result.SchedulesDeactivated,
                result.SchedulesFailed,
                totalDuration.TotalSeconds);

            if (result.SchedulesFailed > 0)
            {
                _logger.LogWarning("⚠️ {FailedCount} recurring transaction schedules failed to process",
                    result.SchedulesFailed);
            }
        }
        catch (Exception ex)
        {
            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "💥 Critical error during daily recurring transaction job after {Duration}s",
                totalDuration.TotalSeconds);
            throw; // Re-throw to trigger Hangfire retry mechanism
        }
    }
}
