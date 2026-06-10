using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Features.RecurringTransactions.Commands;

/// <summary>
/// Auto-create schedules materialize real transactions via CreateTransactionCommand,
/// which requires modify access (owner or Manager share). Viewer-only accounts must
/// be rejected when the schedule is created/updated, not when the nightly job fails.
/// </summary>
public class RecurringTransactionCommandAuthorizationTests
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository =
        Substitute.For<IRecurringTransactionRepository>();
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly ICategoryRepository _categoryRepository = Substitute.For<ICategoryRepository>();
    private readonly IAccountAccessService _accountAccessService = Substitute.For<IAccountAccessService>();

    private readonly Guid _userId = Guid.NewGuid();
    private const int AccountId = 7;

    public RecurringTransactionCommandAuthorizationTests()
    {
        // Account is visible to the user (owner or any accepted share role)
        _accountRepository.GetByIdAsync(AccountId, _userId)
            .Returns(new Account { Id = AccountId, Name = "Shared account" });

        _recurringTransactionRepository.CreateAsync(
                Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<RecurringTransaction>());
        _recurringTransactionRepository.UpdateAsync(
                Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<RecurringTransaction>());
        _recurringTransactionRepository.GetByIdAsync(1, _userId, Arg.Any<CancellationToken>())
            .Returns(ExistingSchedule());
    }

    private CreateRecurringTransactionCommandHandler CreateHandler() =>
        new(_recurringTransactionRepository, _accountRepository, _categoryRepository, _accountAccessService);

    private UpdateRecurringTransactionCommandHandler UpdateHandler() =>
        new(_recurringTransactionRepository, _accountRepository, _categoryRepository, _accountAccessService);

    private CreateRecurringTransactionCommand CreateCommand(bool autoCreate) => new()
    {
        UserId = _userId,
        AccountId = AccountId,
        Description = "Internet bill",
        Amount = 89.99m,
        Frequency = RecurrenceFrequency.Monthly,
        StartDate = DateTime.UtcNow.Date,
        AutoCreate = autoCreate
    };

    private UpdateRecurringTransactionCommand UpdateCommand(bool autoCreate) => new()
    {
        Id = 1,
        UserId = _userId,
        AccountId = AccountId,
        Description = "Internet bill",
        Amount = 89.99m,
        Frequency = RecurrenceFrequency.Monthly,
        StartDate = DateTime.UtcNow.Date,
        AutoCreate = autoCreate
    };

    private RecurringTransaction ExistingSchedule() => new()
    {
        Id = 1,
        UserId = _userId,
        AccountId = AccountId,
        Description = "Internet bill",
        Amount = 89.99m,
        Frequency = RecurrenceFrequency.Monthly,
        StartDate = DateTime.UtcNow.Date,
        NextDueDate = DateTime.UtcNow.Date,
        AutoCreate = false,
        IsActive = true
    };

    private void SetModifyAccess(bool canModify)
    {
        _accountAccessService.CanModifyAccountAsync(_userId, AccountId).Returns(canModify);
    }

    [Fact]
    public async Task Create_AutoCreateOnViewerOnlyAccount_ThrowsUnauthorized()
    {
        SetModifyAccess(false);

        var act = () => CreateHandler().Handle(CreateCommand(autoCreate: true), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _recurringTransactionRepository.DidNotReceive().CreateAsync(
            Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_ReminderOnlyOnViewerOnlyAccount_Succeeds()
    {
        // Reminders only notify the schedule owner — view access is enough.
        SetModifyAccess(false);

        var dto = await CreateHandler().Handle(CreateCommand(autoCreate: false), CancellationToken.None);

        dto.AutoCreate.Should().BeFalse();
        await _recurringTransactionRepository.Received(1).CreateAsync(
            Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_AutoCreateWithModifyAccess_Succeeds()
    {
        SetModifyAccess(true);

        var dto = await CreateHandler().Handle(CreateCommand(autoCreate: true), CancellationToken.None);

        dto.AutoCreate.Should().BeTrue();
        await _recurringTransactionRepository.Received(1).CreateAsync(
            Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_TogglingAutoCreateOnViewerOnlyAccount_ThrowsUnauthorized()
    {
        SetModifyAccess(false);

        var act = () => UpdateHandler().Handle(UpdateCommand(autoCreate: true), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _recurringTransactionRepository.DidNotReceive().UpdateAsync(
            Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ReminderOnlyOnViewerOnlyAccount_Succeeds()
    {
        SetModifyAccess(false);

        var dto = await UpdateHandler().Handle(UpdateCommand(autoCreate: false), CancellationToken.None);

        dto.AutoCreate.Should().BeFalse();
        await _recurringTransactionRepository.Received(1).UpdateAsync(
            Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_AutoCreateWithModifyAccess_Succeeds()
    {
        SetModifyAccess(true);

        var dto = await UpdateHandler().Handle(UpdateCommand(autoCreate: true), CancellationToken.None);

        dto.AutoCreate.Should().BeTrue();
        await _recurringTransactionRepository.Received(1).UpdateAsync(
            Arg.Any<RecurringTransaction>(), Arg.Any<CancellationToken>());
    }
}
