using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.Commands;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reconciliation.Commands;

public class UnlinkTransactionCommandTests
{
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly UnlinkTransactionCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly int _reconciliationId = 1;
    private readonly int _reconciliationItemId = 10;
    private readonly int _transactionId = 100;

    public UnlinkTransactionCommandTests()
    {
        _reconciliationItemRepository = Substitute.For<IReconciliationItemRepository>();
        _reconciliationRepository = Substitute.For<IReconciliationRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();

        // Default: allow modify access on all accounts
        _accountAccessService.CanModifyAccountAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(true);

        _handler = new UnlinkTransactionCommandHandler(
            _reconciliationItemRepository,
            _reconciliationRepository,
            _accountAccessService);
    }

    [Fact]
    public async Task Handle_WithMatchedItem_CreatesUnmatchedItems()
    {
        // Arrange
        var existingItem = CreateMatchedReconciliationItem();
        var reconciliation = CreateTestReconciliation();

        _reconciliationItemRepository.GetByIdAsync(_reconciliationItemId, _userId)
            .Returns(existingItem);
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);

        var command = new UnlinkTransactionCommand
        {
            UserId = _userId,
            ReconciliationItemId = _reconciliationItemId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        // Should delete the existing matched item
        await _reconciliationItemRepository.Received(1).DeleteAsync(existingItem);

        // Should create two new unmatched items (one for system, one for bank)
        await _reconciliationItemRepository.Received(2).AddAsync(Arg.Any<ReconciliationItem>());

        // Verify the unmatched system item
        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Is<ReconciliationItem>(item =>
            item.ReconciliationId == _reconciliationId &&
            item.TransactionId == _transactionId &&
            item.ItemType == ReconciliationItemType.UnmatchedApp &&
            item.MatchConfidence == null &&
            item.MatchMethod == null &&
            item.BankReferenceData == null));

        // Verify the unmatched bank item
        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Is<ReconciliationItem>(item =>
            item.ReconciliationId == _reconciliationId &&
            item.TransactionId == null &&
            item.ItemType == ReconciliationItemType.UnmatchedBank &&
            item.MatchConfidence == null &&
            item.MatchMethod == null &&
            item.BankReferenceData != null));
    }

    [Fact]
    public async Task Handle_WithMatchedItemSystemTransactionOnly_CreatesOnlyUnmatchedSystemItem()
    {
        // Arrange
        var existingItem = CreateMatchedReconciliationItem();
        existingItem.BankReferenceData = null; // No bank transaction data

        var reconciliation = CreateTestReconciliation();

        _reconciliationItemRepository.GetByIdAsync(_reconciliationItemId, _userId)
            .Returns(existingItem);
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);

        var command = new UnlinkTransactionCommand
        {
            UserId = _userId,
            ReconciliationItemId = _reconciliationItemId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        // Should delete the existing matched item
        await _reconciliationItemRepository.Received(1).DeleteAsync(existingItem);

        // Should create only one unmatched item (for system transaction)
        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Any<ReconciliationItem>());

        // Verify the unmatched system item
        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Is<ReconciliationItem>(item =>
            item.ReconciliationId == _reconciliationId &&
            item.TransactionId == _transactionId &&
            item.ItemType == ReconciliationItemType.UnmatchedApp));
    }

    [Fact]
    public async Task Handle_WithMatchedItemBankTransactionOnly_CreatesOnlyUnmatchedBankItem()
    {
        // Arrange
        var existingItem = CreateMatchedReconciliationItem();
        existingItem.TransactionId = null; // No system transaction

        var reconciliation = CreateTestReconciliation();

        _reconciliationItemRepository.GetByIdAsync(_reconciliationItemId, _userId)
            .Returns(existingItem);
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);

        var command = new UnlinkTransactionCommand
        {
            UserId = _userId,
            ReconciliationItemId = _reconciliationItemId
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        // Should delete the existing matched item
        await _reconciliationItemRepository.Received(1).DeleteAsync(existingItem);

        // Should create only one unmatched item (for bank transaction)
        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Any<ReconciliationItem>());

        // Verify the unmatched bank item
        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Is<ReconciliationItem>(item =>
            item.ReconciliationId == _reconciliationId &&
            item.TransactionId == null &&
            item.ItemType == ReconciliationItemType.UnmatchedBank));
    }

    [Fact]
    public async Task Handle_WithNonExistentReconciliationItem_ThrowsArgumentException()
    {
        // Arrange
        _reconciliationItemRepository.GetByIdAsync(_reconciliationItemId, _userId)
            .Returns((ReconciliationItem?)null);

        var command = new UnlinkTransactionCommand
        {
            UserId = _userId,
            ReconciliationItemId = _reconciliationItemId
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithReconciliationNotBelongingToUser_ThrowsArgumentException()
    {
        // Arrange
        var existingItem = CreateMatchedReconciliationItem();

        _reconciliationItemRepository.GetByIdAsync(_reconciliationItemId, _userId)
            .Returns(existingItem);
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns((MyMascada.Domain.Entities.Reconciliation?)null);

        var command = new UnlinkTransactionCommand
        {
            UserId = _userId,
            ReconciliationItemId = _reconciliationItemId
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNonMatchedItem_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingItem = CreateMatchedReconciliationItem();
        existingItem.ItemType = ReconciliationItemType.UnmatchedBank; // Not a matched item

        var reconciliation = CreateTestReconciliation();

        _reconciliationItemRepository.GetByIdAsync(_reconciliationItemId, _userId)
            .Returns(existingItem);
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);

        var command = new UnlinkTransactionCommand
        {
            UserId = _userId,
            ReconciliationItemId = _reconciliationItemId
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _handler.Handle(command, CancellationToken.None));
    }

    private ReconciliationItem CreateMatchedReconciliationItem()
    {
        var bankTransaction = new BankTransactionDto
        {
            BankTransactionId = "BANK_123",
            Amount = -100.50m,
            Description = "Test Bank Transaction",
            TransactionDate = DateTime.UtcNow,
            BankCategory = "TEST_CATEGORY"
        };

        return new ReconciliationItem
        {
            Id = _reconciliationItemId,
            ReconciliationId = _reconciliationId,
            TransactionId = _transactionId,
            ItemType = ReconciliationItemType.Matched,
            MatchConfidence = 0.95m,
            MatchMethod = MatchMethod.Exact,
            BankReferenceData = System.Text.Json.JsonSerializer.Serialize(bankTransaction),
            CreatedBy = _userId.ToString(),
            UpdatedBy = _userId.ToString()
        };
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
}