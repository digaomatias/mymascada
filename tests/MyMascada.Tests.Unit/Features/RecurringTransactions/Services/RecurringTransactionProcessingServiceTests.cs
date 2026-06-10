using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.Services;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Features.RecurringTransactions.Services;

public class RecurringTransactionProcessingServiceTests
{
    private readonly IRecurringTransactionRepository _repository = Substitute.For<IRecurringTransactionRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly INotificationTriggerService _notificationTriggerService = Substitute.For<INotificationTriggerService>();
    private readonly IAccountAccessService _accountAccessService = Substitute.For<IAccountAccessService>();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RecurringTransactionProcessingService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private static readonly DateTime Today = new(2026, 6, 10);

    public RecurringTransactionProcessingServiceTests()
    {
        _service = new RecurringTransactionProcessingService(
            _repository,
            _mediator,
            _notificationTriggerService,
            _accountAccessService,
            _unitOfWork,
            Substitute.For<ILogger<RecurringTransactionProcessingService>>());

        // Default: account exists and the owner retains full access; occurrence
        // claims succeed and echo the claim back.
        _repository.AccountExistsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _accountAccessService.CanModifyAccountAsync(Arg.Any<Guid>(), Arg.Any<int>())
            .Returns(true);
        _accountAccessService.CanAccessAccountAsync(Arg.Any<Guid>(), Arg.Any<int>())
            .Returns(true);
        _repository.TryCreateOccurrenceAsync(
                Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<RecurringTransactionOccurrence>());
        _repository.UpdateOccurrenceAsync(
                Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<RecurringTransactionOccurrence>());
    }

    private RecurringTransaction CreateSchedule(
        bool autoCreate,
        DateTime nextDueDate,
        RecurrenceFrequency frequency = RecurrenceFrequency.Monthly,
        DateTime? endDate = null,
        int id = 1,
        int? customIntervalDays = null)
    {
        return new RecurringTransaction
        {
            Id = id,
            UserId = _userId,
            AccountId = 7,
            CategoryId = 3,
            Description = "Internet bill",
            Amount = 89.99m,
            Frequency = frequency,
            CustomIntervalDays = customIntervalDays,
            StartDate = nextDueDate,
            EndDate = endDate,
            NextDueDate = nextDueDate,
            AutoCreate = autoCreate,
            IsActive = true,
            Notes = "ISP plan"
        };
    }

    private void SetupDue(params RecurringTransaction[] schedules)
    {
        _repository.GetDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(schedules.ToList());
    }

    [Fact]
    public async Task ProcessDueAsync_AutoCreateSchedule_ClaimsThenCreatesPendingNegativeTransaction()
    {
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        SetupDue(schedule);
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionDto { Id = 42 });

        // Snapshot the claim at insert time — the same object is mutated to
        // Created afterwards, so a Received() predicate would see the final state.
        RecurringTransactionOccurrence? claimAtInsert = null;
        _repository.TryCreateOccurrenceAsync(
                Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var occurrence = ci.Arg<RecurringTransactionOccurrence>();
                claimAtInsert = new RecurringTransactionOccurrence
                {
                    RecurringTransactionId = occurrence.RecurringTransactionId,
                    ScheduledDate = occurrence.ScheduledDate,
                    Status = occurrence.Status,
                    TransactionId = occurrence.TransactionId
                };
                return occurrence;
            });

        var result = await _service.ProcessDueAsync(Today);

        result.TransactionsCreated.Should().Be(1);
        result.RemindersSent.Should().Be(0);
        result.SchedulesProcessed.Should().Be(1);

        // The occurrence row is claimed (Pending) BEFORE the transaction is created
        claimAtInsert.Should().NotBeNull();
        claimAtInsert!.RecurringTransactionId.Should().Be(schedule.Id);
        claimAtInsert.ScheduledDate.Should().Be(Today);
        claimAtInsert.Status.Should().Be(RecurringTransactionOccurrenceStatus.Pending);
        claimAtInsert.TransactionId.Should().BeNull();

        await _mediator.Received(1).Send(
            Arg.Is<CreateTransactionCommand>(c =>
                c.UserId == _userId
                && c.AccountId == 7
                && c.CategoryId == 3
                && c.Amount == -89.99m // expenses are stored negative
                && c.Status == TransactionStatus.Pending
                && c.TransactionDate == Today
                && c.Description == "Internet bill"
                && c.AllowDuplicates),
            Arg.Any<CancellationToken>());

        // ... and the claim is completed with the transaction id afterwards
        await _repository.Received(1).UpdateOccurrenceAsync(
            Arg.Is<RecurringTransactionOccurrence>(o =>
                o.Status == RecurringTransactionOccurrenceStatus.Created
                && o.TransactionId == 42),
            Arg.Any<CancellationToken>());

        await _notificationTriggerService.DidNotReceiveWithAnyArgs()
            .NotifyTransactionReminderAsync(default, default!, default, default, default);
    }

    [Fact]
    public async Task ProcessDueAsync_RemindOnlySchedule_SendsReminderInsteadOfCreating()
    {
        var schedule = CreateSchedule(autoCreate: false, nextDueDate: Today);
        SetupDue(schedule);

        var result = await _service.ProcessDueAsync(Today);

        result.RemindersSent.Should().Be(1);
        result.TransactionsCreated.Should().Be(0);

        await _notificationTriggerService.Received(1).NotifyTransactionReminderAsync(
            _userId, "Internet bill", 89.99m, Today, Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive().Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>());

        await _repository.Received(1).UpdateOccurrenceAsync(
            Arg.Is<RecurringTransactionOccurrence>(o =>
                o.Status == RecurringTransactionOccurrenceStatus.Notified
                && o.TransactionId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_AdvancesNextDueDatePastToday()
    {
        var schedule = CreateSchedule(autoCreate: false, nextDueDate: Today);
        SetupDue(schedule);

        await _service.ProcessDueAsync(Today);

        schedule.NextDueDate.Should().Be(new DateTime(2026, 7, 10));
        await _repository.Received(1).UpdateAsync(schedule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_RunTwiceInSameDay_IsIdempotent()
    {
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        SetupDue(schedule);
        _repository.GetOccurrenceAsync(schedule.Id, Today, Arg.Any<CancellationToken>())
            .Returns(new RecurringTransactionOccurrence
            {
                RecurringTransactionId = schedule.Id,
                ScheduledDate = Today,
                Status = RecurringTransactionOccurrenceStatus.Created,
                TransactionId = 42
            }); // already fired by an earlier run

        var result = await _service.ProcessDueAsync(Today);

        result.TransactionsCreated.Should().Be(0);
        result.OccurrencesSkipped.Should().Be(1);

        await _mediator.DidNotReceive().Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().TryCreateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().UpdateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());

        // Schedule is still advanced so it doesn't stay overdue
        schedule.NextDueDate.Should().BeAfter(Today);
        await _repository.Received(1).UpdateAsync(schedule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_ConcurrentRunLosesClaimRace_SkipsWithoutFiring()
    {
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        SetupDue(schedule);
        _repository.TryCreateOccurrenceAsync(
                Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>())
            .Returns((RecurringTransactionOccurrence?)null); // unique index rejected the insert

        var result = await _service.ProcessDueAsync(Today);

        result.TransactionsCreated.Should().Be(0);
        result.OccurrencesSkipped.Should().Be(1);

        await _mediator.DidNotReceive().Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>());
        await _notificationTriggerService.DidNotReceiveWithAnyArgs()
            .NotifyTransactionReminderAsync(default, default!, default, default, default);
    }

    [Fact]
    public async Task ProcessDueAsync_StalePendingClaim_IsCompletedWithoutNewClaim()
    {
        // A previous run crashed after claiming the occurrence but before creating
        // the transaction — the next run must finish it, not fire a duplicate.
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        SetupDue(schedule);
        var staleClaim = new RecurringTransactionOccurrence
        {
            Id = 10,
            RecurringTransactionId = schedule.Id,
            ScheduledDate = Today,
            Status = RecurringTransactionOccurrenceStatus.Pending
        };
        _repository.GetOccurrenceAsync(schedule.Id, Today, Arg.Any<CancellationToken>())
            .Returns(staleClaim);
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionDto { Id = 77 });

        var result = await _service.ProcessDueAsync(Today);

        result.TransactionsCreated.Should().Be(1);
        result.OccurrencesSkipped.Should().Be(0);

        await _repository.DidNotReceive().TryCreateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());

        staleClaim.Status.Should().Be(RecurringTransactionOccurrenceStatus.Created);
        staleClaim.TransactionId.Should().Be(77);
        await _repository.Received(1).UpdateOccurrenceAsync(staleClaim, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_FailureBetweenClaimAndTransaction_SecondRunCreatesExactlyOneTransaction()
    {
        // Simulate two nightly runs against a shared in-memory occurrence store:
        // run 1 claims the occurrence, then the transaction creation fails;
        // run 2 finds the Pending claim and completes it. Exactly one transaction
        // must exist at the end and no duplicate occurrence rows.
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        var store = new Dictionary<DateTime, RecurringTransactionOccurrence>();

        _repository.GetDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ => new List<RecurringTransaction> { schedule });
        _repository.GetOccurrenceAsync(schedule.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci => store.GetValueOrDefault(ci.ArgAt<DateTime>(1).Date));
        _repository.TryCreateOccurrenceAsync(
                Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var occurrence = ci.Arg<RecurringTransactionOccurrence>();
                if (store.ContainsKey(occurrence.ScheduledDate.Date))
                {
                    return null; // unique index violation
                }

                store[occurrence.ScheduledDate.Date] = occurrence;
                return occurrence;
            });

        var failNextSend = true;
        var transactionsCreated = 0;
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns<TransactionDto>(_ =>
            {
                if (failNextSend)
                {
                    // First run: transient failure AFTER the claim was persisted
                    failNextSend = false;
                    throw new InvalidOperationException("transient db failure");
                }

                transactionsCreated++;
                return new TransactionDto { Id = 42 };
            });

        // Run 1 — fails between claim and transaction creation
        var firstRun = await _service.ProcessDueAsync(Today);
        firstRun.SchedulesFailed.Should().Be(1);
        firstRun.TransactionsCreated.Should().Be(0);
        store.Should().HaveCount(1);
        store[Today].Status.Should().Be(RecurringTransactionOccurrenceStatus.Pending);
        _repository.Received(1).ResetChangeTracking();

        // Run 2 — recovers the stale claim instead of replaying the bill
        var secondRun = await _service.ProcessDueAsync(Today);
        secondRun.SchedulesFailed.Should().Be(0);
        secondRun.TransactionsCreated.Should().Be(1);

        transactionsCreated.Should().Be(1); // exactly one real transaction, ever
        store.Should().HaveCount(1); // no duplicate occurrence rows
        store[Today].Status.Should().Be(RecurringTransactionOccurrenceStatus.Created);
        store[Today].TransactionId.Should().Be(42);
    }

    [Fact]
    public async Task ProcessDueAsync_AutoCreate_TransactionInsertAndOccurrenceFinalizeShareOneDbTransaction()
    {
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        SetupDue(schedule);
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _unitOfWork.Events.Add("createTransaction");
                return new TransactionDto { Id = 42 };
            });
        _repository.UpdateOccurrenceAsync(
                Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                _unitOfWork.Events.Add("finalizeOccurrence");
                return ci.Arg<RecurringTransactionOccurrence>();
            });

        await _service.ProcessDueAsync(Today);

        // Both writes happen inside the same ambient transaction, committed after both
        _unitOfWork.Events.Should().Equal("begin", "createTransaction", "finalizeOccurrence", "commit");
    }

    [Fact]
    public async Task ProcessDueAsync_CrashBetweenTransactionInsertAndOccurrenceFinalize_CannotDoubleCreate()
    {
        // Reproduces the reviewed crash window: CreateTransactionCommand succeeds
        // but the process dies / the occurrence update fails before the claim is
        // marked Created. With the ambient DB transaction both writes roll back
        // together, so the recovery run re-creates the transaction exactly once.
        // The fake unit of work mirrors DB semantics: writes staged inside an
        // uncommitted transaction are discarded; reads return fresh copies (the
        // real job detaches everything via ResetChangeTracking after a failure).
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        _repository.GetDueAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(_ => new List<RecurringTransaction> { schedule });

        static RecurringTransactionOccurrence Clone(RecurringTransactionOccurrence o) => new()
        {
            Id = o.Id,
            RecurringTransactionId = o.RecurringTransactionId,
            ScheduledDate = o.ScheduledDate,
            Status = o.Status,
            TransactionId = o.TransactionId
        };

        // Durable store. Claims commit immediately (they are inserted before the
        // ambient transaction begins); reads hand out copies so in-memory mutations
        // by the service cannot leak into "the database".
        var store = new Dictionary<DateTime, RecurringTransactionOccurrence>();
        _repository.GetOccurrenceAsync(schedule.Id, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var row = store.GetValueOrDefault(ci.ArgAt<DateTime>(1).Date);
                return row == null ? null : Clone(row);
            });
        _repository.TryCreateOccurrenceAsync(
                Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var occurrence = ci.Arg<RecurringTransactionOccurrence>();
                if (store.ContainsKey(occurrence.ScheduledDate.Date))
                {
                    return null; // unique index violation
                }

                store[occurrence.ScheduledDate.Date] = Clone(occurrence);
                return occurrence;
            });

        // Writes staged inside the ambient transaction become durable only on commit
        var committedTransactions = 0;
        var stagedTransactions = 0;
        RecurringTransactionOccurrence? stagedOccurrence = null;
        _unitOfWork.OnCommit = () =>
        {
            committedTransactions += stagedTransactions;
            stagedTransactions = 0;
            if (stagedOccurrence != null)
            {
                store[stagedOccurrence.ScheduledDate.Date] = stagedOccurrence;
                stagedOccurrence = null;
            }
        };
        _unitOfWork.OnRollback = () =>
        {
            stagedTransactions = 0;
            stagedOccurrence = null;
        };

        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                stagedTransactions++; // the money record insert SUCCEEDED
                return new TransactionDto { Id = 42 };
            });

        var failNextFinalize = true;
        _repository.UpdateOccurrenceAsync(
                Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                if (failNextFinalize)
                {
                    failNextFinalize = false; // run 1 dies AFTER the transaction insert
                    throw new InvalidOperationException("process died before occurrence finalize");
                }

                stagedOccurrence = Clone(ci.Arg<RecurringTransactionOccurrence>());
                return ci.Arg<RecurringTransactionOccurrence>();
            });

        // Run 1 — transaction inserted, finalize failed → the whole unit rolls back
        var firstRun = await _service.ProcessDueAsync(Today);
        firstRun.SchedulesFailed.Should().Be(1);
        firstRun.TransactionsCreated.Should().Be(0);
        committedTransactions.Should().Be(0, "the rolled-back transaction must not be durable");
        store[Today].Status.Should().Be(RecurringTransactionOccurrenceStatus.Pending);
        store[Today].TransactionId.Should().BeNull();

        // Run 2 — recovers the stale Pending claim and fires exactly once
        var secondRun = await _service.ProcessDueAsync(Today);
        secondRun.SchedulesFailed.Should().Be(0);
        secondRun.TransactionsCreated.Should().Be(1);

        committedTransactions.Should().Be(1); // exactly one durable money record, ever
        store.Should().HaveCount(1); // no duplicate occurrence rows
        store[Today].Status.Should().Be(RecurringTransactionOccurrenceStatus.Created);
        store[Today].TransactionId.Should().Be(42);
    }

    [Fact]
    public async Task ProcessDueAsync_FailedSchedule_ResetsChangeTrackingAndContinues()
    {
        var failing = CreateSchedule(autoCreate: true, nextDueDate: Today, id: 1);
        var healthy = CreateSchedule(autoCreate: false, nextDueDate: Today, id: 2);
        SetupDue(failing, healthy);
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns<TransactionDto>(_ => throw new InvalidOperationException("boom"));

        var result = await _service.ProcessDueAsync(Today);

        result.SchedulesFailed.Should().Be(1);
        result.SchedulesProcessed.Should().Be(1);
        result.RemindersSent.Should().Be(1);

        // The poisoned change tracker is cleared so the healthy schedule can save
        _repository.Received(1).ResetChangeTracking();

        await _notificationTriggerService.Received(1).NotifyTransactionReminderAsync(
            _userId, "Internet bill", 89.99m, Today, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_MissedDays_CatchesUpEachScheduledDate()
    {
        // Weekly schedule that was last due 2 weeks ago: 5/27, 6/3, 6/10 are all due
        var schedule = CreateSchedule(
            autoCreate: true,
            nextDueDate: new DateTime(2026, 5, 27),
            frequency: RecurrenceFrequency.Weekly);
        SetupDue(schedule);
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionDto { Id = 42 });

        var result = await _service.ProcessDueAsync(Today);

        result.TransactionsCreated.Should().Be(3);
        await _repository.Received(3).TryCreateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());
        await _repository.Received(3).UpdateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 17));
    }

    [Fact]
    public async Task ProcessDueAsync_MoreDueDatesThanCatchUpCap_ContinuesFromLastFiredDateInsteadOfSkipping()
    {
        // Daily schedule 400 days behind: one run can only materialize
        // MaxCatchUpIterations (366) occurrences. The schedule must then point
        // at the day AFTER the last fired occurrence — still in the past, so
        // the next nightly run picks it up and keeps catching up — instead of
        // snapping past today and silently losing the remaining 34 days.
        var firstDue = Today.AddDays(-400);
        var schedule = CreateSchedule(
            autoCreate: true,
            nextDueDate: firstDue,
            frequency: RecurrenceFrequency.Custom,
            customIntervalDays: 1);
        SetupDue(schedule);
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionDto { Id = 42 });

        var result = await _service.ProcessDueAsync(Today);

        result.TransactionsCreated.Should().Be(RecurringTransaction.MaxCatchUpIterations);

        // Last fired date = firstDue + 365 days; next due = the day after it
        var expectedNextDue = firstDue.AddDays(RecurringTransaction.MaxCatchUpIterations);
        schedule.NextDueDate.Should().Be(expectedNextDue);
        schedule.NextDueDate.Should().BeOnOrBefore(Today, "the remaining occurrences must stay due for the next run");
        schedule.IsActive.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(schedule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_SchedulePastEndDate_IsDeactivated()
    {
        var schedule = CreateSchedule(
            autoCreate: false,
            nextDueDate: Today,
            frequency: RecurrenceFrequency.Weekly,
            endDate: Today); // last occurrence is today

        SetupDue(schedule);

        var result = await _service.ProcessDueAsync(Today);

        result.RemindersSent.Should().Be(1);
        result.SchedulesDeactivated.Should().Be(1);
        schedule.IsActive.Should().BeFalse();
        await _repository.Received(1).UpdateAsync(schedule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_AccountSoftDeleted_DeactivatesScheduleWithoutFiring()
    {
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        SetupDue(schedule);
        _repository.AccountExistsAsync(schedule.AccountId, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _service.ProcessDueAsync(Today);

        result.SchedulesDeactivated.Should().Be(1);
        result.TransactionsCreated.Should().Be(0);
        result.RemindersSent.Should().Be(0);
        schedule.IsActive.Should().BeFalse();

        await _mediator.DidNotReceive().Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>());
        await _notificationTriggerService.DidNotReceiveWithAnyArgs()
            .NotifyTransactionReminderAsync(default, default!, default, default, default);
        await _repository.DidNotReceive().TryCreateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(schedule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_AutoCreateWithModifyAccessRevoked_PausesScheduleWithoutFiring()
    {
        // Share revoked/downgraded after the schedule was created: auto-create
        // would fail CreateTransactionCommand's modify-access check every night.
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        SetupDue(schedule);
        _accountAccessService.CanModifyAccountAsync(_userId, schedule.AccountId).Returns(false);

        var result = await _service.ProcessDueAsync(Today);

        result.SchedulesDeactivated.Should().Be(1);
        result.TransactionsCreated.Should().Be(0);
        result.SchedulesFailed.Should().Be(0);
        schedule.IsActive.Should().BeFalse();

        await _mediator.DidNotReceive().Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().TryCreateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(schedule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_RemindOnlyWithViewAccessRevoked_PausesScheduleWithoutGhostReminder()
    {
        // The user can no longer see the account at all — keeping the nightly
        // reminder alive would notify about an account they have no access to.
        var schedule = CreateSchedule(autoCreate: false, nextDueDate: Today);
        SetupDue(schedule);
        _accountAccessService.CanAccessAccountAsync(_userId, schedule.AccountId).Returns(false);

        var result = await _service.ProcessDueAsync(Today);

        result.SchedulesDeactivated.Should().Be(1);
        result.RemindersSent.Should().Be(0);
        schedule.IsActive.Should().BeFalse();

        await _notificationTriggerService.DidNotReceiveWithAnyArgs()
            .NotifyTransactionReminderAsync(default, default!, default, default, default);
        await _repository.DidNotReceive().TryCreateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(schedule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_RemindOnlyDowngradedToViewer_StillSendsReminder()
    {
        // Viewer access is enough for reminder-only schedules — only modify
        // access was lost, so the reminder keeps firing.
        var schedule = CreateSchedule(autoCreate: false, nextDueDate: Today);
        SetupDue(schedule);
        _accountAccessService.CanModifyAccountAsync(_userId, schedule.AccountId).Returns(false);

        var result = await _service.ProcessDueAsync(Today);

        result.RemindersSent.Should().Be(1);
        result.SchedulesDeactivated.Should().Be(0);
        schedule.IsActive.Should().BeTrue();

        await _notificationTriggerService.Received(1).NotifyTransactionReminderAsync(
            _userId, "Internet bill", 89.99m, Today, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessDueAsync_NoDueSchedules_DoesNothing()
    {
        SetupDue();

        var result = await _service.ProcessDueAsync(Today);

        result.SchedulesProcessed.Should().Be(0);
        result.TransactionsCreated.Should().Be(0);
        result.RemindersSent.Should().Be(0);
        await _repository.DidNotReceive().UpdateAsync(
            Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Hand-rolled IUnitOfWork fake: records begin/commit/rollback events so tests
    /// can assert what runs inside the ambient transaction, and exposes OnCommit /
    /// OnRollback hooks so tests can simulate database durability (writes staged
    /// inside an uncommitted transaction are discarded on rollback).
    /// </summary>
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public List<string> Events { get; } = new();
        public Action? OnCommit { get; set; }
        public Action? OnRollback { get; set; }

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            Events.Add("begin");
            return Task.FromResult<IUnitOfWorkTransaction>(new FakeTransaction(this));
        }

        private sealed class FakeTransaction : IUnitOfWorkTransaction
        {
            private readonly FakeUnitOfWork _owner;
            private bool _committed;

            public FakeTransaction(FakeUnitOfWork owner) => _owner = owner;

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                _committed = true;
                _owner.Events.Add("commit");
                _owner.OnCommit?.Invoke();
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                if (!_committed)
                {
                    _owner.Events.Add("rollback");
                    _owner.OnRollback?.Invoke();
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
