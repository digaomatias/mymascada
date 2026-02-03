using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringPatterns.Services;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-based implementation of recurring pattern background jobs.
/// Runs daily to detect patterns and process missed payments for all users.
/// </summary>
public class RecurringPatternJobService : IRecurringPatternJobService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RecurringPatternJobService> _logger;

    public RecurringPatternJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<RecurringPatternJobService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Processes all users' recurring patterns - detects new patterns and handles missed payments.
    /// Scheduled to run daily at 2:00 AM.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ProcessAllUsersAsync(object? performContext = null)
    {
        var startTime = DateTime.UtcNow;
        var totalUsersProcessed = 0;
        var totalPatternsDetected = 0;
        var totalMissedPaymentsProcessed = 0;
        var failedUsers = new List<Guid>();

        _logger.LogInformation("🔄 Starting daily recurring pattern job at {StartTime}", startTime);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var patternRepository = scope.ServiceProvider.GetRequiredService<IRecurringPatternRepository>();
            var persistenceService = scope.ServiceProvider.GetRequiredService<IRecurringPatternPersistenceService>();

            // Get all users who have transactions
            var userIds = await patternRepository.GetUserIdsWithTransactionsAsync();
            var userIdList = userIds.ToList();

            _logger.LogInformation("📊 Found {UserCount} users with transactions to process", userIdList.Count);

            foreach (var userId in userIdList)
            {
                try
                {
                    _logger.LogDebug("Processing user {UserId}...", userId);

                    // Detect and persist patterns
                    var patternsDetected = await persistenceService.DetectAndPersistPatternsAsync(userId);
                    totalPatternsDetected += patternsDetected;

                    // Process missed payments
                    var missedPayments = await persistenceService.ProcessMissedPaymentsAsync(userId);
                    totalMissedPaymentsProcessed += missedPayments;

                    totalUsersProcessed++;

                    if ((totalUsersProcessed % 10) == 0)
                    {
                        _logger.LogInformation("📊 Progress: {ProcessedCount}/{TotalCount} users processed",
                            totalUsersProcessed, userIdList.Count);
                    }
                }
                catch (Exception ex)
                {
                    failedUsers.Add(userId);
                    _logger.LogError(ex, "❌ Failed to process recurring patterns for user {UserId}", userId);
                }
            }

            var totalDuration = DateTime.UtcNow - startTime;

            _logger.LogInformation("✅ Daily recurring pattern job completed at {EndTime}. " +
                "📊 Stats: {UsersProcessed} users, {PatternsDetected} patterns detected/updated, " +
                "{MissedPayments} missed payments processed, {FailedUsers} failed users. " +
                "⏱️ Duration: {Duration}s",
                DateTime.UtcNow,
                totalUsersProcessed,
                totalPatternsDetected,
                totalMissedPaymentsProcessed,
                failedUsers.Count,
                totalDuration.TotalSeconds);

            if (failedUsers.Any())
            {
                _logger.LogWarning("⚠️ Failed to process {FailedCount} users: [{FailedUserIds}]",
                    failedUsers.Count,
                    string.Join(", ", failedUsers.Take(10).Select(id => id.ToString())));
            }
        }
        catch (Exception ex)
        {
            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "💥 Critical error during daily recurring pattern job after {Duration}s. " +
                "📊 Progress: {ProcessedCount} users processed before failure",
                totalDuration.TotalSeconds,
                totalUsersProcessed);
            throw; // Re-throw to trigger Hangfire retry mechanism
        }
    }

    /// <summary>
    /// Processes recurring patterns for a specific user.
    /// Useful for manual triggering or testing.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task ProcessUserAsync(Guid userId, object? performContext = null)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("🔄 Starting recurring pattern job for user {UserId} at {StartTime}",
            userId, startTime);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var persistenceService = scope.ServiceProvider.GetRequiredService<IRecurringPatternPersistenceService>();

            // Detect and persist patterns
            var patternsDetected = await persistenceService.DetectAndPersistPatternsAsync(userId);

            // Process missed payments
            var missedPayments = await persistenceService.ProcessMissedPaymentsAsync(userId);

            var totalDuration = DateTime.UtcNow - startTime;

            _logger.LogInformation("✅ Recurring pattern job completed for user {UserId}. " +
                "📊 Stats: {PatternsDetected} patterns detected/updated, {MissedPayments} missed payments processed. " +
                "⏱️ Duration: {Duration}s",
                userId,
                patternsDetected,
                missedPayments,
                totalDuration.TotalSeconds);
        }
        catch (Exception ex)
        {
            var totalDuration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "💥 Failed recurring pattern job for user {UserId} after {Duration}s",
                userId, totalDuration.TotalSeconds);
            throw; // Re-throw to trigger Hangfire retry mechanism
        }
    }
}
