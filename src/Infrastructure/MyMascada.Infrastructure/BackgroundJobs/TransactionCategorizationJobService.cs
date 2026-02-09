using Hangfire;
using Hangfire.Server;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Features.Transactions.Commands;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-based implementation of transaction categorization background jobs
/// </summary>
public class TransactionCategorizationJobService : ITransactionCategorizationJobService
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TransactionCategorizationJobService> _logger;

    public TransactionCategorizationJobService(
        IBackgroundJobClient backgroundJobClient,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TransactionCategorizationJobService> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public string EnqueueCategorization(List<int> transactionIds, string userId)
    {
        var jobId = _backgroundJobClient.Enqueue("categorization", () => ProcessCategorizationAsync(transactionIds, userId, null));

        _logger.LogInformation("Enqueued transaction categorization job {JobId} for {TransactionCount} transactions for user {UserId}",
            jobId, transactionIds.Count, userId);

        return jobId;
    }

    public string EnqueueCategorizationAfter(string parentJobId, List<int> transactionIds, string userId)
    {
        var jobId = _backgroundJobClient.ContinueJobWith(parentJobId,
            () => ProcessCategorizationAsync(transactionIds, userId, null));

        _logger.LogInformation(
            "Enqueued categorization continuation job {JobId} after parent job {ParentJobId} for {TransactionCount} transactions for user {UserId}",
            jobId, parentJobId, transactionIds.Count, userId);

        return jobId;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 })]
    public async Task ProcessCategorizationAsync(List<int> transactionIds, string userId, object? performContext = null)
    {
        var totalTransactions = transactionIds.Count;
        var startTime = DateTime.UtcNow;
        var processedCount = 0;
        var successCount = 0;
        var failedCount = 0;
        var failedTransactionIds = new List<int>();

        _logger.LogInformation("🚀 Starting background categorization job for {TransactionCount} transactions for user {UserId} at {StartTime}", 
            totalTransactions, userId, startTime);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            for (int i = 0; i < transactionIds.Count; i++)
            {
                var transactionId = transactionIds[i];
                var transactionStartTime = DateTime.UtcNow;
                
                try
                {
                    _logger.LogDebug("🔄 Processing transaction {TransactionId} ({Current}/{Total}) for user {UserId}", 
                        transactionId, i + 1, totalTransactions, userId);

                    var command = new CategorizeTransactionCommand 
                    { 
                        TransactionId = transactionId,
                        UserId = Guid.Parse(userId)
                    };
                    await mediator.Send(command);
                    
                    successCount++;
                    var transactionDuration = DateTime.UtcNow - transactionStartTime;
                    
                    _logger.LogInformation("✅ Successfully categorized transaction {TransactionId} for user {UserId} in {Duration}ms", 
                        transactionId, userId, transactionDuration.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    failedTransactionIds.Add(transactionId);
                    var transactionDuration = DateTime.UtcNow - transactionStartTime;
                    
                    _logger.LogError(ex, "❌ Failed to categorize transaction {TransactionId} for user {UserId} after {Duration}ms - {ErrorMessage}", 
                        transactionId, userId, transactionDuration.TotalMilliseconds, ex.Message);
                }
                finally
                {
                    processedCount++;
                    
                    // Log progress every 5 transactions or at the end
                    if (processedCount % 5 == 0 || processedCount == totalTransactions)
                    {
                        _logger.LogInformation("📊 Progress update: {ProcessedCount}/{TotalTransactions} transactions processed " +
                                             "(✅ {SuccessCount} success, ❌ {FailedCount} failed)", 
                                             processedCount, totalTransactions, successCount, failedCount);
                    }
                }
            }

            var totalDuration = DateTime.UtcNow - startTime;
            var avgTimePerTransaction = totalDuration.TotalSeconds / totalTransactions;

            // Final summary log
            _logger.LogInformation("🎯 Completed background categorization job for user {UserId} - " +
                                 "📊 Stats: {TotalTransactions} total, {SuccessCount} successful, {FailedCount} failed, " +
                                 "⏱️ Duration: {TotalDuration}s, Avg: {AvgTime}s per transaction", 
                userId, totalTransactions, successCount, failedCount, 
                totalDuration.TotalSeconds, avgTimePerTransaction);

            // Log final summary with enhanced details
            if (failedTransactionIds.Any())
            {
                _logger.LogWarning("⚠️ Some transactions failed during categorization for user {UserId}: [{FailedIds}]",
                    userId, string.Join(", ", failedTransactionIds));
            }
        }
        catch (Exception ex)
        {
            var totalDuration = DateTime.UtcNow - startTime;
            
            _logger.LogError(ex, "💥 Critical error during background categorization for user {UserId} after {Duration}s - " +
                           "📊 Progress: {ProcessedCount}/{TotalCount} transactions processed", 
                userId, totalDuration.TotalSeconds, processedCount, totalTransactions);
            
            _logger.LogError("💥 Additional error details: Progress {ProcessedCount}/{TotalTransactions}, Duration: {TotalDuration}s", 
                processedCount, totalTransactions, totalDuration.TotalSeconds);
            
            throw; // Re-throw to trigger Hangfire retry mechanism
        }
    }
}