using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringPatterns.Services;

namespace MyMascada.Application.Events.Handlers;

/// <summary>
/// Handles TransactionsCreatedEvent by enqueuing Hangfire background jobs for description cleaning
/// (if enabled) and categorization, and matching transactions to recurring patterns.
/// Uses proper queuing with retry logic, monitoring, and persistence.
/// </summary>
public class TransactionsCreatedEventHandler : INotificationHandler<TransactionsCreatedEvent>
{
    private readonly ITransactionCategorizationJobService _jobService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRecurringPatternPersistenceService? _patternPersistenceService;
    private readonly IDescriptionCleaningJobService? _descriptionCleaningJobService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<TransactionsCreatedEventHandler> _logger;

    public TransactionsCreatedEventHandler(
        ITransactionCategorizationJobService jobService,
        ITransactionRepository transactionRepository,
        ILogger<TransactionsCreatedEventHandler> logger,
        IUserRepository userRepository,
        IRecurringPatternPersistenceService? patternPersistenceService = null,
        IDescriptionCleaningJobService? descriptionCleaningJobService = null)
    {
        _jobService = jobService;
        _transactionRepository = transactionRepository;
        _patternPersistenceService = patternPersistenceService;
        _descriptionCleaningJobService = descriptionCleaningJobService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task Handle(TransactionsCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.TransactionIds.Any())
        {
            _logger.LogWarning("No transaction IDs provided in TransactionsCreatedEvent for user {UserId}",
                notification.UserId);
            return;
        }

        // Check if the user has AI description cleaning enabled
        var useDescriptionCleaning = false;
        if (_descriptionCleaningJobService != null)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(notification.UserId);
                useDescriptionCleaning = user?.AiDescriptionCleaning == true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check user description cleaning preference for user {UserId}",
                    notification.UserId);
            }
        }

        if (useDescriptionCleaning)
        {
            // Enqueue description cleaning first, then chain categorization after it completes
            var cleaningJobId = _descriptionCleaningJobService!.EnqueueDescriptionCleaning(
                notification.TransactionIds, notification.UserId.ToString());

            _logger.LogInformation(
                "Enqueued description cleaning job {JobId} for {TransactionCount} transactions for user {UserId}",
                cleaningJobId, notification.TransactionIds.Count, notification.UserId);

            // Chain categorization to run after description cleaning completes
            var categorizationJobId = _jobService.EnqueueCategorizationAfter(
                cleaningJobId, notification.TransactionIds, notification.UserId.ToString());

            _logger.LogInformation(
                "Chained categorization job {JobId} after cleaning job {CleaningJobId} for user {UserId}",
                categorizationJobId, cleaningJobId, notification.UserId);
        }
        else
        {
            // Enqueue categorization directly (current behavior)
            var jobId = _jobService.EnqueueCategorization(notification.TransactionIds, notification.UserId.ToString());

            _logger.LogInformation(
                "Enqueued Hangfire categorization job {JobId} for {TransactionCount} transactions for user {UserId}",
                jobId, notification.TransactionIds.Count, notification.UserId);
        }

        // Try to match transactions to recurring patterns (if persistence service is available)
        if (_patternPersistenceService != null)
        {
            await TryMatchTransactionsToPatterns(notification.TransactionIds, notification.UserId, cancellationToken);
        }
    }

    /// <summary>
    /// Attempts to match new transactions to existing recurring patterns
    /// </summary>
    private async Task TryMatchTransactionsToPatterns(
        List<int> transactionIds,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load the transactions
            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, userId, cancellationToken);
            var matchedCount = 0;

            foreach (var transaction in transactions)
            {
                try
                {
                    var matched = await _patternPersistenceService!.TryMatchTransactionToPatternAsync(
                        transaction, cancellationToken);

                    if (matched)
                    {
                        matchedCount++;
                        _logger.LogDebug("Transaction {TransactionId} matched to a recurring pattern",
                            transaction.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check pattern match for transaction {TransactionId}",
                        transaction.Id);
                }
            }

            if (matchedCount > 0)
            {
                _logger.LogInformation("Matched {MatchedCount} of {TotalCount} transactions to recurring patterns for user {UserId}",
                    matchedCount, transactionIds.Count, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to match transactions to patterns for user {UserId}", userId);
            // Don't rethrow - this is a non-critical enhancement
        }
    }
}