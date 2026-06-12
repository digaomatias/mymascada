using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.Commands;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reconciliation.Commands;

public class CreateReconciliationCommandTests
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly CreateReconciliationCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly int _accountId = 1;

    public CreateReconciliationCommandTests()
    {
        _reconciliationRepository = Substitute.For<IReconciliationRepository>();
        _auditLogRepository = Substitute.For<IReconciliationAuditLogRepository>();
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();

        _accountRepository.GetByIdAsync(_accountId, _userId)
            .Returns(new Account { Id = _accountId, UserId = _userId, Name = "Test Account" });
        _accountAccessService.CanModifyAccountAsync(_userId, _accountId).Returns(true);
        _transactionRepository.GetAccountBalanceAsync(_accountId, _userId).Returns(1000m);

        // Echo back the entity passed to AddAsync (simulating a save that assigns an ID)
        _reconciliationRepository.AddAsync(Arg.Any<MyMascada.Domain.Entities.Reconciliation>())
            .Returns(callInfo =>
            {
                var reconciliation = callInfo.Arg<MyMascada.Domain.Entities.Reconciliation>();
                reconciliation.Id = 1;
                return reconciliation;
            });

        _handler = new CreateReconciliationCommandHandler(
            _reconciliationRepository,
            _auditLogRepository,
            _accountRepository,
            _transactionRepository,
            _accountAccessService);
    }

    [Fact]
    public async Task Handle_WithUnspecifiedKindStatementEndDate_StoresDateAsUtc()
    {
        // Arrange — mobile clients send ISO dates without timezone info (e.g. "2026-06-12T00:00:00.000"),
        // which ASP.NET parses as DateTimeKind.Unspecified. Npgsql rejects Unspecified for timestamptz.
        var unspecifiedDate = DateTime.Parse("2026-06-12T00:00:00.000");
        unspecifiedDate.Kind.Should().Be(DateTimeKind.Unspecified, "precondition: parsed date must be Unspecified");

        var command = new CreateReconciliationCommand
        {
            UserId = _userId,
            AccountId = _accountId,
            StatementEndDate = unspecifiedDate,
            StatementEndBalance = 950m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — the date must reach the repository with Kind=Utc and the same wall-clock value
        await _reconciliationRepository.Received(1).AddAsync(Arg.Is<MyMascada.Domain.Entities.Reconciliation>(r =>
            r.StatementEndDate.Kind == DateTimeKind.Utc &&
            r.StatementEndDate == DateTime.SpecifyKind(unspecifiedDate, DateTimeKind.Utc)));

        result.StatementEndDate.Kind.Should().Be(DateTimeKind.Utc);
        result.StatementEndDate.Should().Be(DateTime.SpecifyKind(unspecifiedDate, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_WithLocalKindStatementEndDate_ConvertsToUniversalTime()
    {
        // Arrange
        var localDate = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Local);

        var command = new CreateReconciliationCommand
        {
            UserId = _userId,
            AccountId = _accountId,
            StatementEndDate = localDate,
            StatementEndBalance = 950m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _reconciliationRepository.Received(1).AddAsync(Arg.Is<MyMascada.Domain.Entities.Reconciliation>(r =>
            r.StatementEndDate.Kind == DateTimeKind.Utc &&
            r.StatementEndDate == localDate.ToUniversalTime()));

        result.StatementEndDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Handle_WithUtcKindStatementEndDate_PreservesValue()
    {
        // Arrange
        var utcDate = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc);

        var command = new CreateReconciliationCommand
        {
            UserId = _userId,
            AccountId = _accountId,
            StatementEndDate = utcDate,
            StatementEndBalance = 950m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _reconciliationRepository.Received(1).AddAsync(Arg.Is<MyMascada.Domain.Entities.Reconciliation>(r =>
            r.StatementEndDate.Kind == DateTimeKind.Utc &&
            r.StatementEndDate == utcDate));

        result.StatementEndDate.Should().Be(utcDate);
    }

    [Fact]
    public async Task Handle_WithNonExistentAccount_ThrowsArgumentException()
    {
        // Arrange
        _accountRepository.GetByIdAsync(_accountId, _userId).Returns((Account?)null);

        var command = new CreateReconciliationCommand
        {
            UserId = _userId,
            AccountId = _accountId,
            StatementEndDate = DateTime.UtcNow,
            StatementEndBalance = 950m
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}
