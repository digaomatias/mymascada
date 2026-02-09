using Hangfire;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Features.DescriptionCleaning.Commands;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-based implementation of description cleaning background jobs
/// </summary>
public class DescriptionCleaningJobService : IDescriptionCleaningJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<DescriptionCleaningJobService> _logger;

    public DescriptionCleaningJobService(
        IBackgroundJobClient backgroundJobClient,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DescriptionCleaningJobService> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public string EnqueueDescriptionCleaning(List<int> transactionIds, string userId)
    {
        var jobId = _backgroundJobClient.Enqueue("description-cleaning",
            () => ProcessDescriptionCleaningAsync(transactionIds, userId, null));

        _logger.LogInformation(
            "Enqueued description cleaning job {JobId} for {TransactionCount} transactions for user {UserId}",
            jobId, transactionIds.Count, userId);

        return jobId;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task ProcessDescriptionCleaningAsync(
        List<int> transactionIds,
        string userId,
        object? performContext = null)
    {
        var totalTransactions = transactionIds.Count;
        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Starting background description cleaning job for {TransactionCount} transactions for user {UserId}",
            totalTransactions, userId);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var command = new CleanTransactionDescriptionsCommand
            {
                UserId = Guid.Parse(userId),
                TransactionIds = transactionIds
            };

            var result = await mediator.Send(command);

            var totalDuration = DateTime.UtcNow - startTime;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Completed description cleaning job for user {UserId} - " +
                    "Total: {TotalTransactions}, Cleaned: {CleanedCount}, Skipped: {SkippedCount}, " +
                    "Duration: {TotalDuration}s",
                    userId, result.TotalTransactions, result.CleanedTransactions,
                    result.SkippedTransactions, totalDuration.TotalSeconds);
            }
            else
            {
                _logger.LogWarning(
                    "Description cleaning job completed with errors for user {UserId} - " +
                    "Total: {TotalTransactions}, Cleaned: {CleanedCount}, Errors: {ErrorCount}, " +
                    "Duration: {TotalDuration}s",
                    userId, result.TotalTransactions, result.CleanedTransactions,
                    result.Errors.Count, totalDuration.TotalSeconds);
            }
        }
        catch (Exception ex)
        {
            var totalDuration = DateTime.UtcNow - startTime;

            _logger.LogError(ex,
                "Critical error during background description cleaning for user {UserId} after {Duration}s",
                userId, totalDuration.TotalSeconds);

            throw; // Re-throw to trigger Hangfire retry mechanism
        }
    }
}
