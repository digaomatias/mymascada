using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringTransactions.Services;

/// <summary>
/// Result summary of a recurring transaction processing run
/// </summary>
public class RecurringTransactionProcessingResult
{
    public int SchedulesProcessed { get; set; }
    public int TransactionsCreated { get; set; }
    public int RemindersSent { get; set; }
    public int OccurrencesSkipped { get; set; }
    public int SchedulesDeactivated { get; set; }
    public int SchedulesFailed { get; set; }
}

/// <summary>
/// Core logic for the daily recurring transaction job. For every active schedule
/// that is due, either auto-creates the real transaction or sends a reminder,
/// then advances the schedule. Idempotent per (schedule, scheduled date).
/// </summary>
public interface IRecurringTransactionProcessingService
{
    Task<RecurringTransactionProcessingResult> ProcessDueAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default);
}

public class RecurringTransactionProcessingService : IRecurringTransactionProcessingService
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository;
    private readonly IMediator _mediator;
    private readonly INotificationTriggerService _notificationTriggerService;
    private readonly ILogger<RecurringTransactionProcessingService> _logger;

    public RecurringTransactionProcessingService(
        IRecurringTransactionRepository recurringTransactionRepository,
        IMediator mediator,
        INotificationTriggerService notificationTriggerService,
        ILogger<RecurringTransactionProcessingService> logger)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
        _mediator = mediator;
        _notificationTriggerService = notificationTriggerService;
        _logger = logger;
    }

    public async Task<RecurringTransactionProcessingResult> ProcessDueAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var today = asOfDate.Date;
        var result = new RecurringTransactionProcessingResult();

        var dueSchedules = (await _recurringTransactionRepository.GetDueAsync(today, cancellationToken)).ToList();

        _logger.LogInformation(
            "Processing {Count} due recurring transactions as of {Date}",
            dueSchedules.Count, today);

        foreach (var schedule in dueSchedules)
        {
            try
            {
                await ProcessScheduleAsync(schedule, today, result, cancellationToken);
                result.SchedulesProcessed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.SchedulesFailed++;
                _logger.LogError(ex,
                    "Failed to process recurring transaction {RecurringTransactionId} for user {UserId}",
                    schedule.Id, schedule.UserId);

                // A failed save leaves poisoned entries in the shared scoped change
                // tracker; detach everything so subsequent schedules can still save.
                _recurringTransactionRepository.ResetChangeTracking();
            }
        }

        return result;
    }

    private async Task ProcessScheduleAsync(
        RecurringTransaction schedule,
        DateTime today,
        RecurringTransactionProcessingResult result,
        CancellationToken cancellationToken)
    {
        // Schedules pointing at a deleted account can never succeed — deactivate
        // them instead of failing (or ghost-reminding) every night.
        if (!await _recurringTransactionRepository.AccountExistsAsync(schedule.AccountId, cancellationToken))
        {
            _logger.LogWarning(
                "Account {AccountId} for recurring transaction {RecurringTransactionId} no longer exists; deactivating schedule",
                schedule.AccountId, schedule.Id);

            schedule.Pause();
            await _recurringTransactionRepository.UpdateAsync(schedule, cancellationToken);
            result.SchedulesDeactivated++;
            return;
        }

        foreach (var dueDate in schedule.GetDueDates(today))
        {
            var existing = await _recurringTransactionRepository.GetOccurrenceAsync(
                schedule.Id, dueDate, cancellationToken);

            if (existing != null)
            {
                if (existing.Status == RecurringTransactionOccurrenceStatus.Pending)
                {
                    // A previous run claimed this date but crashed before completing it.
                    // Concurrent job executions are serialized (DisableConcurrentExecution),
                    // so a Pending row seen here is stale — finish it now.
                    await CompleteOccurrenceAsync(schedule, existing, dueDate, result, cancellationToken);
                }
                else
                {
                    // Already fired on an earlier run — never fire the same date twice.
                    result.OccurrencesSkipped++;
                }

                continue;
            }

            // Claim the scheduled date BEFORE creating the transaction / sending the
            // reminder. The unique (RecurringTransactionId, ScheduledDate) index makes
            // the claim atomic: a crash after this point leaves a Pending row that the
            // next run completes, instead of replaying the bill into a duplicate.
            var claim = await _recurringTransactionRepository.TryCreateOccurrenceAsync(
                new RecurringTransactionOccurrence
                {
                    RecurringTransactionId = schedule.Id,
                    ScheduledDate = dueDate,
                    Status = RecurringTransactionOccurrenceStatus.Pending
                }, cancellationToken);

            if (claim == null)
            {
                // Lost the insert race to another run that claimed this date.
                result.OccurrencesSkipped++;
                continue;
            }

            await CompleteOccurrenceAsync(schedule, claim, dueDate, result, cancellationToken);
        }

        var wasActive = schedule.IsActive;
        schedule.AdvanceNextDueDate(today);

        if (wasActive && !schedule.IsActive)
        {
            result.SchedulesDeactivated++;
        }

        await _recurringTransactionRepository.UpdateAsync(schedule, cancellationToken);
    }

    private async Task CompleteOccurrenceAsync(
        RecurringTransaction schedule,
        RecurringTransactionOccurrence occurrence,
        DateTime dueDate,
        RecurringTransactionProcessingResult result,
        CancellationToken cancellationToken)
    {
        if (schedule.AutoCreate)
        {
            // Reuse the standard transaction creation pipeline (validation,
            // categorization, account access). Expenses are stored negative.
            var transactionDto = await _mediator.Send(new CreateTransactionCommand
            {
                UserId = schedule.UserId,
                AccountId = schedule.AccountId,
                CategoryId = schedule.CategoryId,
                Amount = -Math.Abs(schedule.Amount),
                TransactionDate = dueDate,
                Description = schedule.Description,
                Status = TransactionStatus.Pending,
                Notes = schedule.Notes,
                Tags = "recurring",
                // The claimed occurrence row is our idempotency mechanism; the duplicate
                // checker would otherwise collapse identical catch-up occurrences.
                AllowDuplicates = true
            }, cancellationToken);

            occurrence.TransactionId = transactionDto.Id;
            occurrence.Status = RecurringTransactionOccurrenceStatus.Created;
            result.TransactionsCreated++;
        }
        else
        {
            await _notificationTriggerService.NotifyTransactionReminderAsync(
                schedule.UserId,
                schedule.Description,
                schedule.Amount,
                dueDate,
                cancellationToken);

            occurrence.Status = RecurringTransactionOccurrenceStatus.Notified;
            result.RemindersSent++;
        }

        await _recurringTransactionRepository.UpdateOccurrenceAsync(occurrence, cancellationToken);
    }
}
