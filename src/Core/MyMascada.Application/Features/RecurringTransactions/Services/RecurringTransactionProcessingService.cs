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
    private readonly IAccountAccessService _accountAccessService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecurringTransactionProcessingService> _logger;

    public RecurringTransactionProcessingService(
        IRecurringTransactionRepository recurringTransactionRepository,
        IMediator mediator,
        INotificationTriggerService notificationTriggerService,
        IAccountAccessService accountAccessService,
        IUnitOfWork unitOfWork,
        ILogger<RecurringTransactionProcessingService> logger)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
        _mediator = mediator;
        _notificationTriggerService = notificationTriggerService;
        _accountAccessService = accountAccessService;
        _unitOfWork = unitOfWork;
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

        // A revoked or downgraded share leaves the account in place but the owner
        // without the access the schedule needs: auto-create requires modify access
        // (CreateTransactionCommand enforces it and would fail every night), and a
        // reminder-only schedule must not keep notifying about an account the user
        // can no longer view. Pause instead of failing/ghost-reminding forever.
        var hasRequiredAccess = schedule.AutoCreate
            ? await _accountAccessService.CanModifyAccountAsync(schedule.UserId, schedule.AccountId)
            : await _accountAccessService.CanAccessAccountAsync(schedule.UserId, schedule.AccountId);

        if (!hasRequiredAccess)
        {
            _logger.LogWarning(
                "User {UserId} no longer has {RequiredAccess} access to account {AccountId} for recurring transaction {RecurringTransactionId}; pausing schedule",
                schedule.UserId, schedule.AutoCreate ? "modify" : "view", schedule.AccountId, schedule.Id);

            schedule.Pause();
            await _recurringTransactionRepository.UpdateAsync(schedule, cancellationToken);
            result.SchedulesDeactivated++;
            return;
        }

        var dueDates = schedule.GetDueDates(today);

        foreach (var dueDate in dueDates)
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

        if (dueDates.Count > 0)
        {
            // Advance from the last materialized date instead of snapping past
            // today: when GetDueDates hits its catch-up cap the remaining dates
            // are still due, and the next nightly run continues from where this
            // one stopped instead of silently losing them.
            schedule.AdvanceNextDueDateAfter(dueDates[^1]);
        }
        else
        {
            schedule.AdvanceNextDueDate(today);
        }

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
            // The real transaction insert and the occurrence's Pending → Created
            // flip must commit atomically. Every repository in this job scope
            // shares the same scoped DbContext, so the handler's internal
            // SaveChanges enlists in this ambient transaction. A crash or failed
            // occurrence update after CreateTransactionCommand therefore rolls
            // the money record back too, instead of leaving a committed
            // transaction behind a Pending claim for the next run's recovery
            // path to duplicate.
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

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
            await _recurringTransactionRepository.UpdateOccurrenceAsync(occurrence, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            result.TransactionsCreated++;
        }
        else
        {
            // Reminders are not money records: a rare duplicate notification is
            // benign and the push delivery cannot be rolled back anyway, so no
            // ambient transaction is needed here.
            await _notificationTriggerService.NotifyTransactionReminderAsync(
                schedule.UserId,
                schedule.Description,
                schedule.Amount,
                dueDate,
                cancellationToken);

            occurrence.Status = RecurringTransactionOccurrenceStatus.Notified;
            result.RemindersSent++;

            await _recurringTransactionRepository.UpdateOccurrenceAsync(occurrence, cancellationToken);
        }
    }
}
