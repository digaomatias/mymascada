using MyMascada.Domain.Enums;
using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.Commands;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Services;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reconciliation.Commands;

public class ManualMatchTransactionCommandTests
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMatchConfidenceCalculator _matchConfidenceCalculator;
    private readonly IAccountAccessService _accountAccessService;
    private readonly ManualMatchTransactionCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly int _reconciliationId = 1;
    private readonly int _transactionId = 100;

    public ManualMatchTransactionCommandTests()
    {
        _reconciliationRepository = Substitute.For<IReconciliationRepository>();
        _reconciliationItemRepository = Substitute.For<IReconciliationItemRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _matchConfidenceCalculator = Substitute.For<IMatchConfidenceCalculator>();
        _accountAccessService = Substitute.For<IAccountAccessService>();

        // Default: allow modify access on all accounts
        _accountAccessService.CanModifyAccountAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(true);

        _handler = new ManualMatchTransactionCommandHandler(
            _reconciliationRepository,
            _reconciliationItemRepository,
            _transactionRepository,
            _matchConfidenceCalculator,
            _accountAccessService);
    }

    [Fact]
    public async Task Handle_WithValidSystemTransactionAndBankTransaction_CreatesMatchedItem()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var systemTransaction = CreateTestTransaction();
        var bankTransaction = CreateTestBankTransaction();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _transactionRepository.GetByIdAsync(_transactionId, _userId)
            .Returns(systemTransaction);
        _matchConfidenceCalculator.CalculateMatchConfidence(systemTransaction, bankTransaction)
            .Returns(0.85m);
        _matchConfidenceCalculator.AnalyzeMatch(systemTransaction, bankTransaction)
            .Returns(CreateTestMatchAnalysis());

        var command = new ManualMatchTransactionCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            SystemTransactionId = _transactionId,
            BankTransaction = bankTransaction,
            Notes = "Manual match by user"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ReconciliationId.Should().Be(_reconciliationId);
        result.TransactionId.Should().Be(_transactionId);
        result.ItemType.Should().Be(ReconciliationItemType.Matched);
        result.MatchMethod.Should().Be(MatchMethod.Manual);
        result.MatchConfidence.Should().Be(0.85m);
        result.BankTransaction.Should().BeEquivalentTo(bankTransaction);
        result.SystemTransaction.Should().NotBeNull();
        result.MatchAnalysis.Should().NotBeNull();

        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Any<ReconciliationItem>());
    }

    [Fact]
    public async Task Handle_WithOnlySystemTransaction_CreatesUnmatchedAppItem()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var systemTransaction = CreateTestTransaction();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _transactionRepository.GetByIdAsync(_transactionId, _userId)
            .Returns(systemTransaction);

        var command = new ManualMatchTransactionCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            SystemTransactionId = _transactionId,
            BankTransaction = null,
            Notes = "System transaction only"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ItemType.Should().Be(ReconciliationItemType.UnmatchedApp);
        result.MatchMethod.Should().Be(MatchMethod.Manual);
        result.MatchConfidence.Should().BeNull();
        result.BankTransaction.Should().BeNull();
        result.SystemTransaction.Should().NotBeNull();

        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Any<ReconciliationItem>());
    }

    [Fact]
    public async Task Handle_WithOnlyBankTransaction_CreatesUnmatchedBankItem()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var bankTransaction = CreateTestBankTransaction();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);

        var command = new ManualMatchTransactionCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            SystemTransactionId = null,
            BankTransaction = bankTransaction,
            Notes = "Bank transaction only"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ItemType.Should().Be(ReconciliationItemType.UnmatchedBank);
        result.MatchMethod.Should().Be(MatchMethod.Manual);
        result.MatchConfidence.Should().BeNull();
        result.BankTransaction.Should().BeEquivalentTo(bankTransaction);
        result.SystemTransaction.Should().BeNull();

        await _reconciliationItemRepository.Received(1).AddAsync(Arg.Any<ReconciliationItem>());
    }

    [Fact]
    public async Task Handle_WithNonExistentReconciliation_ThrowsArgumentException()
    {
        // Arrange
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns((MyMascada.Domain.Entities.Reconciliation?)null);

        var command = new ManualMatchTransactionCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            SystemTransactionId = _transactionId,
            BankTransaction = CreateTestBankTransaction()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNonExistentTransaction_ThrowsArgumentException()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _transactionRepository.GetByIdAsync(_transactionId, _userId)
            .Returns((Transaction?)null);

        var command = new ManualMatchTransactionCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            SystemTransactionId = _transactionId,
            BankTransaction = CreateTestBankTransaction()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNeitherSystemNorBankTransaction_ThrowsArgumentException()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);

        var command = new ManualMatchTransactionCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            SystemTransactionId = null,
            BankTransaction = null
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.Handle(command, CancellationToken.None));
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

    private Transaction CreateTestTransaction()
    {
        return new Transaction
        {
            Id = _transactionId,
            AccountId = 1,
            Amount = -100.50m,
            Description = "Test Transaction",
            TransactionDate = DateTime.UtcNow,
            Status = TransactionStatus.Cleared,
            CreatedBy = _userId.ToString(),
            UpdatedBy = _userId.ToString()
        };
    }

    private BankTransactionDto CreateTestBankTransaction()
    {
        return new BankTransactionDto
        {
            BankTransactionId = "BANK_123",
            Amount = -100.50m,
            Description = "Test Bank Transaction",
            TransactionDate = DateTime.UtcNow,
            BankCategory = "TEST_CATEGORY"
        };
    }

    private MatchAnalysisDto CreateTestMatchAnalysis()
    {
        return new MatchAnalysisDto
        {
            AmountMatch = true,
            DateMatch = true,
            DescriptionSimilar = true,
            AmountDifference = 0,
            DateDifferenceInDays = 0,
            DescriptionSimilarityScore = 0.95m,
            SystemAmount = -100.50m,
            BankAmount = -100.50m,
            SystemDate = DateTime.UtcNow,
            BankDate = DateTime.UtcNow,
            SystemDescription = "Test Transaction",
            BankDescription = "Test Bank Transaction"
        };
    }
}