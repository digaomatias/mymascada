using MyMascada.Domain.Enums;
using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Queries;
using MyMascada.Application.Features.Reconciliation.Services;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reconciliation.Queries;

public class GetReconciliationDetailsQueryTests
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMatchConfidenceCalculator _matchConfidenceCalculator;
    private readonly GetReconciliationDetailsQueryHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly int _reconciliationId = 1;

    public GetReconciliationDetailsQueryTests()
    {
        _reconciliationRepository = Substitute.For<IReconciliationRepository>();
        _reconciliationItemRepository = Substitute.For<IReconciliationItemRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _matchConfidenceCalculator = Substitute.For<IMatchConfidenceCalculator>();
        _handler = new GetReconciliationDetailsQueryHandler(
            _reconciliationRepository,
            _reconciliationItemRepository,
            _transactionRepository,
            _matchConfidenceCalculator);
            
        // Set up default mock behavior for match confidence calculator
        _matchConfidenceCalculator.AnalyzeMatch(Arg.Any<Transaction>(), Arg.Any<BankTransactionDto>())
            .Returns(new MatchAnalysisDto
            {
                AmountMatch = true,
                DateMatch = true,
                DescriptionSimilar = true,
                AmountDifference = 0,
                DateDifferenceInDays = 0,
                DescriptionSimilarityScore = 0.95m,
                SystemAmount = -100m,
                BankAmount = -100m,
                SystemDate = DateTime.UtcNow,
                BankDate = DateTime.UtcNow,
                SystemDescription = "Test Transaction",
                BankDescription = "Test Transaction"
            });
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsReconciliationDetails()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateTestReconciliationItems();
        var transactions = CreateTestTransactions();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetTransactionsByIdsAsync(Arg.Any<List<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        var query = new GetReconciliationDetailsQuery
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ReconciliationId.Should().Be(_reconciliationId);
        result.Summary.Should().NotBeNull();
        result.Summary.TotalItems.Should().Be(4);
        result.Summary.ExactMatches.Should().Be(1);
        result.Summary.FuzzyMatches.Should().Be(1);
        result.Summary.UnmatchedBank.Should().Be(1);
        result.Summary.UnmatchedSystem.Should().Be(1);
        result.Summary.MatchPercentage.Should().Be(50); // 2 matches out of 4 total
    }

    [Fact]
    public async Task Handle_WithSearchTerm_FiltersCorrectly()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateTestReconciliationItems();
        var transactions = CreateTestTransactions();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetTransactionsByIdsAsync(Arg.Any<List<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        var query = new GetReconciliationDetailsQuery
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            SearchTerm = "grocery"
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var allItems = result.ExactMatches.Concat(result.FuzzyMatches)
            .Concat(result.UnmatchedBankTransactions)
            .Concat(result.UnmatchedSystemTransactions);
        
        allItems.Should().AllSatisfy(item => 
            item.DisplayDescription.ToLowerInvariant().Should().Contain("grocery"));
    }

    [Fact]
    public async Task Handle_WithAmountFilter_FiltersCorrectly()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateTestReconciliationItems();
        var transactions = CreateTestTransactions();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetTransactionsByIdsAsync(Arg.Any<List<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        var query = new GetReconciliationDetailsQuery
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinAmount = 50m,
            MaxAmount = 200m
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        var allItems = result.ExactMatches.Concat(result.FuzzyMatches)
            .Concat(result.UnmatchedBankTransactions)
            .Concat(result.UnmatchedSystemTransactions);
        
        allItems.Should().AllSatisfy(item =>
        {
            var amount = item.SystemTransaction?.Amount ?? item.BankTransaction?.Amount ?? 0;
            Math.Abs(amount).Should().BeInRange(50m, 200m);
        });
    }

    [Fact]
    public async Task Handle_WithNonExistentReconciliation_ThrowsArgumentException()
    {
        // Arrange
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns((MyMascada.Domain.Entities.Reconciliation?)null);

        var query = new GetReconciliationDetailsQuery
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithTypeFilter_FiltersCorrectly()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateTestReconciliationItems();
        var transactions = CreateTestTransactions();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetTransactionsByIdsAsync(Arg.Any<List<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        var query = new GetReconciliationDetailsQuery
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            FilterByType = ReconciliationItemType.UnmatchedBank
        };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.UnmatchedBankTransactions.Should().HaveCount(1);
        result.ExactMatches.Should().BeEmpty();
        result.FuzzyMatches.Should().BeEmpty();
        result.UnmatchedSystemTransactions.Should().BeEmpty();
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

    private List<ReconciliationItem> CreateTestReconciliationItems()
    {
        return new List<ReconciliationItem>
        {
            // Exact match
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                TransactionId = 1,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.98m,
                MatchMethod = MatchMethod.Exact,
                BankReferenceData = System.Text.Json.JsonSerializer.Serialize(new BankTransactionDto
                {
                    BankTransactionId = "bank1",
                    Amount = -100m,
                    TransactionDate = DateTime.UtcNow,
                    Description = "Grocery Store",
                    BankCategory = "Food"
                }),
                CreatedBy = _userId.ToString(),
                UpdatedBy = _userId.ToString()
            },
            // Fuzzy match
            new ReconciliationItem
            {
                Id = 2,
                ReconciliationId = _reconciliationId,
                TransactionId = 2,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.75m,
                MatchMethod = MatchMethod.Fuzzy,
                BankReferenceData = System.Text.Json.JsonSerializer.Serialize(new BankTransactionDto
                {
                    BankTransactionId = "bank2",
                    Amount = -50.50m,
                    TransactionDate = DateTime.UtcNow.AddDays(-1),
                    Description = "Gas Station ABC",
                    BankCategory = "Gas"
                }),
                CreatedBy = _userId.ToString(),
                UpdatedBy = _userId.ToString()
            },
            // Unmatched bank
            new ReconciliationItem
            {
                Id = 3,
                ReconciliationId = _reconciliationId,
                ItemType = ReconciliationItemType.UnmatchedBank,
                BankReferenceData = System.Text.Json.JsonSerializer.Serialize(new BankTransactionDto
                {
                    BankTransactionId = "bank3",
                    Amount = -75m,
                    TransactionDate = DateTime.UtcNow.AddDays(-2),
                    Description = "Restaurant XYZ",
                    BankCategory = "Dining"
                }),
                CreatedBy = _userId.ToString(),
                UpdatedBy = _userId.ToString()
            },
            // Unmatched system
            new ReconciliationItem
            {
                Id = 4,
                ReconciliationId = _reconciliationId,
                TransactionId = 3,
                ItemType = ReconciliationItemType.UnmatchedApp,
                CreatedBy = _userId.ToString(),
                UpdatedBy = _userId.ToString()
            }
        };
    }

    private List<Transaction> CreateTestTransactions()
    {
        return new List<Transaction>
        {
            new Transaction
            {
                Id = 1,
                AccountId = 1,
                Amount = -100m,
                Description = "Grocery Store",
                TransactionDate = DateTime.UtcNow,
                Status = TransactionStatus.Cleared,
                CreatedBy = _userId.ToString(),
                UpdatedBy = _userId.ToString()
            },
            new Transaction
            {
                Id = 2,
                AccountId = 1,
                Amount = -50m,
                Description = "Gas Station",
                TransactionDate = DateTime.UtcNow.AddDays(-1),
                Status = TransactionStatus.Cleared,
                CreatedBy = _userId.ToString(),
                UpdatedBy = _userId.ToString()
            },
            new Transaction
            {
                Id = 3,
                AccountId = 1,
                Amount = -200m,
                Description = "Unknown Transaction",
                TransactionDate = DateTime.UtcNow.AddDays(-3),
                Status = TransactionStatus.Cleared,
                CreatedBy = _userId.ToString(),
                UpdatedBy = _userId.ToString()
            }
        };
    }
}