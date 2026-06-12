using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reconciliation.Commands;

public class UpdateReconciliationCommandTests
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly UpdateReconciliationCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly int _reconciliationId = 1;
    private readonly int _accountId = 1;

    public UpdateReconciliationCommandTests()
    {
        _reconciliationRepository = Substitute.For<IReconciliationRepository>();
        _auditLogRepository = Substitute.For<IReconciliationAuditLogRepository>();
        _accountRepository = Substitute.For<IAccountRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();

        _accountRepository.GetByIdAsync(_accountId, _userId)
            .Returns(new Account { Id = _accountId, UserId = _userId, Name = "Test Account" });
        _accountAccessService.CanModifyAccountAsync(_userId, _accountId).Returns(true);

        _handler = new UpdateReconciliationCommandHandler(
            _reconciliationRepository,
            _auditLogRepository,
            _accountRepository,
            _accountAccessService);
    }

    [Fact]
    public async Task Handle_WithUnspecifiedKindStatementEndDate_StoresDateAsUtc()
    {
        // Arrange — unsuffixed ISO dates parse as DateTimeKind.Unspecified, which Npgsql rejects for timestamptz
        var reconciliation = CreateTestReconciliation();
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId).Returns(reconciliation);

        var unspecifiedDate = DateTime.Parse("2026-06-12T00:00:00.000");
        unspecifiedDate.Kind.Should().Be(DateTimeKind.Unspecified, "precondition: parsed date must be Unspecified");

        var command = new UpdateReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            StatementEndDate = unspecifiedDate
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _reconciliationRepository.Received(1).UpdateAsync(Arg.Is<MyMascada.Domain.Entities.Reconciliation>(r =>
            r.StatementEndDate.Kind == DateTimeKind.Utc &&
            r.StatementEndDate == DateTime.SpecifyKind(unspecifiedDate, DateTimeKind.Utc)));

        result.StatementEndDate.Kind.Should().Be(DateTimeKind.Utc);
        result.StatementEndDate.Should().Be(DateTime.SpecifyKind(unspecifiedDate, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_WithLocalKindStatementEndDate_ConvertsToUniversalTime()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId).Returns(reconciliation);

        var localDate = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Local);

        var command = new UpdateReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            StatementEndDate = localDate
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _reconciliationRepository.Received(1).UpdateAsync(Arg.Is<MyMascada.Domain.Entities.Reconciliation>(r =>
            r.StatementEndDate.Kind == DateTimeKind.Utc &&
            r.StatementEndDate == localDate.ToUniversalTime()));

        result.StatementEndDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Handle_WithoutStatementEndDate_KeepsExistingDate()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var originalDate = reconciliation.StatementEndDate;
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId).Returns(reconciliation);

        var command = new UpdateReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            Notes = "Just updating notes"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.StatementEndDate.Should().Be(originalDate);
        result.Notes.Should().Be("Just updating notes");
    }

    private MyMascada.Domain.Entities.Reconciliation CreateTestReconciliation()
    {
        return new MyMascada.Domain.Entities.Reconciliation
        {
            Id = _reconciliationId,
            AccountId = _accountId,
            StatementEndDate = DateTime.UtcNow,
            StatementEndBalance = 1000m,
            Status = ReconciliationStatus.InProgress,
            CreatedBy = _userId.ToString(),
            UpdatedBy = _userId.ToString()
        };
    }
}
