using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reconciliation.Commands;

public class FinalizeReconciliationCommandTests
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly IReconciliationAuditLogRepository _auditLogRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly FinalizeReconciliationCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly int _reconciliationId = 1;

    public FinalizeReconciliationCommandTests()
    {
        _reconciliationRepository = Substitute.For<IReconciliationRepository>();
        _reconciliationItemRepository = Substitute.For<IReconciliationItemRepository>();
        _auditLogRepository = Substitute.For<IReconciliationAuditLogRepository>();
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();

        // Default: allow modify access on all accounts
        _accountAccessService.CanModifyAccountAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(true);

        _handler = new FinalizeReconciliationCommandHandler(
            _reconciliationRepository,
            _reconciliationItemRepository,
            _auditLogRepository,
            _accountRepository,
            _transactionRepository,
            _accountAccessService);
    }

    [Fact]
    public async Task Handle_WithAllItemsMatched_FinalizesSuccessfully()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateAllMatchedItems();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            Notes = "All transactions successfully matched"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(_reconciliationId);
        result.Status.Should().Be(ReconciliationStatus.Completed);
        result.CompletedAt.Should().NotBeNull();

        // Verify reconciliation was updated
        await _reconciliationRepository.Received(1).UpdateAsync(Arg.Is<MyMascada.Domain.Entities.Reconciliation>(r => 
            r.Status == ReconciliationStatus.Completed && r.CompletedAt != null));

        // Verify audit log was created
        await _auditLogRepository.Received(1).AddAsync(Arg.Any<ReconciliationAuditLog>());
    }

    [Fact]
    public async Task Handle_WithUnmatchedItemsWithinThreshold_FinalizesSuccessfully()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateItemsWithinThreshold(); // 95% matched (within 5% threshold)

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ReconciliationStatus.Completed);

        await _reconciliationRepository.Received(1).UpdateAsync(Arg.Any<MyMascada.Domain.Entities.Reconciliation>());
        await _auditLogRepository.Received(1).AddAsync(Arg.Any<ReconciliationAuditLog>());
    }

    [Fact]
    public async Task Handle_WithTooManyUnmatchedItems_ThrowsInvalidOperationException()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateItemsAboveThreshold(); // 50% unmatched (above 5% threshold)

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            ForceFinalize = false
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("Too many unmatched items");
    }

    [Fact]
    public async Task Handle_WithForceFinalize_FinalizesRegardlessOfUnmatchedItems()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateItemsAboveThreshold(); // 50% unmatched

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            ForceFinalize = true,
            Notes = "Force finalized due to manual review"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ReconciliationStatus.Completed);

        await _reconciliationRepository.Received(1).UpdateAsync(Arg.Any<MyMascada.Domain.Entities.Reconciliation>());
        await _auditLogRepository.Received(1).AddAsync(Arg.Any<ReconciliationAuditLog>());
    }

    [Fact]
    public async Task Handle_WithAlreadyCompletedReconciliation_ThrowsInvalidOperationException()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        reconciliation.Status = ReconciliationStatus.Completed;

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("already completed");
    }

    [Fact]
    public async Task Handle_WithNonExistentReconciliation_ThrowsArgumentException()
    {
        // Arrange
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns((MyMascada.Domain.Entities.Reconciliation?)null);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UpdatesAccountLastReconciledDateAndBalance()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateAllMatchedItems();
        var account = new Account
        {
            Id = 1,
            UserId = _userId,
            Name = "Test Account",
            CurrentBalance = 1000m,
            LastReconciledDate = null,
            LastReconciledBalance = null
        };

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _accountRepository.GetByIdAsync(reconciliation.AccountId, _userId)
            .Returns(account);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ReconciliationStatus.Completed);

        // Verify account was updated with reconciliation info
        account.LastReconciledDate.Should().Be(reconciliation.StatementEndDate);
        account.LastReconciledBalance.Should().Be(reconciliation.StatementEndBalance);

        await _accountRepository.Received(1).UpdateAsync(Arg.Is<Account>(a =>
            a.LastReconciledDate == reconciliation.StatementEndDate &&
            a.LastReconciledBalance == reconciliation.StatementEndBalance));
    }

    [Fact]
    public async Task Handle_MarksMatchedTransactionsAsReconciled()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateAllMatchedItems();

        // Create transactions that will be marked as reconciled
        var transaction1 = new Transaction
        {
            Id = 1,
            AccountId = reconciliation.AccountId,
            Amount = -50m,
            Description = "Test transaction 1",
            Status = TransactionStatus.Cleared
        };
        var transaction2 = new Transaction
        {
            Id = 2,
            AccountId = reconciliation.AccountId,
            Amount = -75m,
            Description = "Test transaction 2",
            Status = TransactionStatus.Cleared
        };

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetByIdAsync(1, _userId).Returns(transaction1);
        _transactionRepository.GetByIdAsync(2, _userId).Returns(transaction2);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ReconciliationStatus.Completed);

        // Verify transactions were updated to Reconciled status
        transaction1.Status.Should().Be(TransactionStatus.Reconciled);
        transaction2.Status.Should().Be(TransactionStatus.Reconciled);

        await _transactionRepository.Received(2).UpdateAsync(Arg.Is<Transaction>(t =>
            t.Status == TransactionStatus.Reconciled));
    }

    [Fact]
    public async Task Handle_DoesNotUpdateAlreadyReconciledTransactions()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateAllMatchedItems();

        // One transaction already reconciled, one not
        var transaction1 = new Transaction
        {
            Id = 1,
            AccountId = reconciliation.AccountId,
            Amount = -50m,
            Description = "Already reconciled",
            Status = TransactionStatus.Reconciled
        };
        var transaction2 = new Transaction
        {
            Id = 2,
            AccountId = reconciliation.AccountId,
            Amount = -75m,
            Description = "Not reconciled",
            Status = TransactionStatus.Cleared
        };

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetByIdAsync(1, _userId).Returns(transaction1);
        _transactionRepository.GetByIdAsync(2, _userId).Returns(transaction2);

        var command = new FinalizeReconciliationCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - only one transaction should be updated (the one that wasn't already reconciled)
        await _transactionRepository.Received(1).UpdateAsync(Arg.Is<Transaction>(t =>
            t.Id == 2 && t.Status == TransactionStatus.Reconciled));
    }

    private MyMascada.Domain.Entities.Reconciliation CreateTestReconciliation()
    {
        return new MyMascada.Domain.Entities.Reconciliation
        {
            Id = _reconciliationId,
            AccountId = 1,
            StatementEndDate = DateTime.UtcNow,
            StatementEndBalance = 1000m,
            Status = ReconciliationStatus.InProgress,
            CreatedBy = _userId.ToString(),
            UpdatedBy = _userId.ToString()
        };
    }

    private List<ReconciliationItem> CreateAllMatchedItems()
    {
        return new List<ReconciliationItem>
        {
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                TransactionId = 1,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.98m
            },
            new ReconciliationItem
            {
                Id = 2,
                ReconciliationId = _reconciliationId,
                TransactionId = 2,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.95m
            }
        };
    }

    private List<ReconciliationItem> CreateItemsWithinThreshold()
    {
        // 19 matched, 1 unmatched = 95% match rate (within 5% threshold)
        var items = new List<ReconciliationItem>();
        
        for (int i = 1; i <= 19; i++)
        {
            items.Add(new ReconciliationItem
            {
                Id = i,
                ReconciliationId = _reconciliationId,
                TransactionId = i,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.95m
            });
        }

        items.Add(new ReconciliationItem
        {
            Id = 20,
            ReconciliationId = _reconciliationId,
            ItemType = ReconciliationItemType.UnmatchedBank,
            MatchConfidence = null
        });

        return items;
    }

    private List<ReconciliationItem> CreateItemsAboveThreshold()
    {
        // 2 matched, 2 unmatched = 50% match rate (above 5% threshold)
        return new List<ReconciliationItem>
        {
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                TransactionId = 1,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.95m
            },
            new ReconciliationItem
            {
                Id = 2,
                ReconciliationId = _reconciliationId,
                TransactionId = 2,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.92m
            },
            new ReconciliationItem
            {
                Id = 3,
                ReconciliationId = _reconciliationId,
                ItemType = ReconciliationItemType.UnmatchedBank,
                MatchConfidence = null
            },
            new ReconciliationItem
            {
                Id = 4,
                ReconciliationId = _reconciliationId,
                TransactionId = 3,
                ItemType = ReconciliationItemType.UnmatchedApp,
                MatchConfidence = null
            }
        };
    }
}