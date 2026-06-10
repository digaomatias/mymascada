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
    private readonly RecurringTransactionProcessingService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private static readonly DateTime Today = new(2026, 6, 10);

    public RecurringTransactionProcessingServiceTests()
    {
        _service = new RecurringTransactionProcessingService(
            _repository,
            _mediator,
            _notificationTriggerService,
            Substitute.For<ILogger<RecurringTransactionProcessingService>>());
    }

    private RecurringTransaction CreateSchedule(
        bool autoCreate,
        DateTime nextDueDate,
        RecurrenceFrequency frequency = RecurrenceFrequency.Monthly,
        DateTime? endDate = null,
        int id = 1)
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
    public async Task ProcessDueAsync_AutoCreateSchedule_CreatesPendingNegativeTransaction()
    {
        var schedule = CreateSchedule(autoCreate: true, nextDueDate: Today);
        SetupDue(schedule);
        _mediator.Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TransactionDto { Id = 42 });

        var result = await _service.ProcessDueAsync(Today);

        result.TransactionsCreated.Should().Be(1);
        result.RemindersSent.Should().Be(0);
        result.SchedulesProcessed.Should().Be(1);

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

        await _repository.Received(1).CreateOccurrenceAsync(
            Arg.Is<RecurringTransactionOccurrence>(o =>
                o.RecurringTransactionId == schedule.Id
                && o.ScheduledDate == Today
                && o.Status == RecurringTransactionOccurrenceStatus.Created
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

        await _repository.Received(1).CreateOccurrenceAsync(
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
        _repository.HasOccurrenceAsync(schedule.Id, Today, Arg.Any<CancellationToken>())
            .Returns(true); // occurrence already recorded by an earlier run

        var result = await _service.ProcessDueAsync(Today);

        result.TransactionsCreated.Should().Be(0);
        result.OccurrencesSkipped.Should().Be(1);

        await _mediator.DidNotReceive().Send(Arg.Any<CreateTransactionCommand>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().CreateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());

        // Schedule is still advanced so it doesn't stay overdue
        schedule.NextDueDate.Should().BeAfter(Today);
        await _repository.Received(1).UpdateAsync(schedule, Arg.Any<CancellationToken>());
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
        await _repository.Received(3).CreateOccurrenceAsync(
            Arg.Any<RecurringTransactionOccurrence>(), Arg.Any<CancellationToken>());

        schedule.NextDueDate.Should().Be(new DateTime(2026, 6, 17));
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
    public async Task ProcessDueAsync_OneScheduleFails_OthersStillProcessed()
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
}
