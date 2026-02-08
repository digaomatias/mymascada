using FluentAssertions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reconciliation.Commands;

public class BulkApproveMatchesCommandTests
{
    private readonly IReconciliationRepository _reconciliationRepository;
    private readonly IReconciliationItemRepository _reconciliationItemRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IApplicationLogger<BulkApproveMatchesCommandHandler> _logger;
    private readonly BulkApproveMatchesCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly int _reconciliationId = 1;

    public BulkApproveMatchesCommandTests()
    {
        _reconciliationRepository = Substitute.For<IReconciliationRepository>();
        _reconciliationItemRepository = Substitute.For<IReconciliationItemRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();
        _logger = Substitute.For<IApplicationLogger<BulkApproveMatchesCommandHandler>>();

        // Default: allow modify access on all accounts
        _accountAccessService.CanModifyAccountAsync(Arg.Any<Guid>(), Arg.Any<int>()).Returns(true);

        _handler = new BulkApproveMatchesCommandHandler(
            _reconciliationRepository,
            _reconciliationItemRepository,
            _transactionRepository,
            _accountAccessService,
            _logger);
    }

    [Fact]
    public async Task Handle_WithConfidenceThreshold_ApprovesMatchingItems()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateTestReconciliationItems();
        var transactions = CreateTestTransactions();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        // Setup transaction lookups
        foreach (var tx in transactions)
        {
            _transactionRepository.GetByIdAsync(tx.Id, _userId).Returns(tx);
        }

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinConfidenceThreshold = 0.90m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Should approve 2 matched items with confidence >= 0.90 (items 1 and 2)
        result.ApprovedCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithSpecificItemIds_ApprovesOnlySpecifiedItems()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateTestReconciliationItems();
        var transactions = CreateTestTransactions();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        foreach (var tx in transactions)
        {
            _transactionRepository.GetByIdAsync(tx.Id, _userId).Returns(tx);
        }

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            SpecificItemIds = new[] { 1, 2 } // Only specific items
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Should approve only the 2 specified matched items
        result.ApprovedCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithHighConfidenceThreshold_ApprovesFewerItems()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = CreateTestReconciliationItems();
        var transactions = CreateTestTransactions();

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        foreach (var tx in transactions)
        {
            _transactionRepository.GetByIdAsync(tx.Id, _userId).Returns(tx);
        }

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinConfidenceThreshold = 0.98m // Very high threshold
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Should approve only 1 item with confidence >= 0.98
        result.ApprovedCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNonExistentReconciliation_ThrowsArgumentException()
    {
        // Arrange
        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns((MyMascada.Domain.Entities.Reconciliation?)null);

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinConfidenceThreshold = 0.95m
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithNoMatchingItems_ReturnsZeroApproved()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = new List<ReconciliationItem>
        {
            // Only unmatched items
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                ItemType = ReconciliationItemType.UnmatchedBank,
                MatchConfidence = null
            },
            new ReconciliationItem
            {
                Id = 2,
                ReconciliationId = _reconciliationId,
                ItemType = ReconciliationItemType.UnmatchedApp,
                MatchConfidence = null
            }
        };

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinConfidenceThreshold = 0.95m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ApprovedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithBankCategory_SetsBankCategoryOnTransaction()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = new List<ReconciliationItem>
        {
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                TransactionId = 1,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.98m,
                MatchMethod = MatchMethod.Exact,
                BankReferenceData = "{\"externalId\":\"ext123\",\"category\":\"Groceries\",\"amount\":-50.00}"
            }
        };

        var transaction = new Transaction
        {
            Id = 1,
            AccountId = 1,
            Amount = -50.00m,
            Description = "Test transaction",
            BankCategory = null // Not set yet
        };

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetByIdAsync(1, _userId).Returns(transaction);

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinConfidenceThreshold = 0.95m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ApprovedCount.Should().Be(1);
        result.EnrichedCount.Should().Be(1);
        // BankCategory is now set for later processing by categorization pipeline
        transaction.BankCategory.Should().Be("Groceries");
    }

    [Fact]
    public async Task Handle_EnrichesTransactionWithBankData()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = new List<ReconciliationItem>
        {
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                TransactionId = 1,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.98m,
                MatchMethod = MatchMethod.Exact,
                BankReferenceData = "{\"externalId\":\"ext123\",\"reference\":\"REF456\",\"category\":\"Shopping\",\"amount\":-50.00}"
            }
        };

        var transaction = new Transaction
        {
            Id = 1,
            AccountId = 1,
            Amount = -50.00m,
            Description = "Test transaction",
            ExternalId = null, // Will be enriched
            ReferenceNumber = null, // Will be enriched
            BankCategory = null // Will be enriched
        };

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetByIdAsync(1, _userId).Returns(transaction);

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinConfidenceThreshold = 0.95m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ApprovedCount.Should().Be(1);
        result.EnrichedCount.Should().Be(1);
        transaction.ExternalId.Should().Be("ext123");
        transaction.ReferenceNumber.Should().Be("REF456");
        transaction.BankCategory.Should().Be("Shopping");
    }

    [Fact]
    public async Task Handle_SkipsAlreadyApprovedItems()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = new List<ReconciliationItem>
        {
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                TransactionId = 1,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.98m,
                MatchMethod = MatchMethod.Exact,
                IsApproved = true, // Already approved
                ApprovedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinConfidenceThreshold = 0.95m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ApprovedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SetsTransactionStatusToReconciled()
    {
        // Arrange
        var reconciliation = CreateTestReconciliation();
        var items = new List<ReconciliationItem>
        {
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                TransactionId = 1,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.98m,
                MatchMethod = MatchMethod.Exact,
                BankReferenceData = "{\"externalId\":\"ext123\",\"amount\":-50.00}"
            }
        };

        var transaction = new Transaction
        {
            Id = 1,
            AccountId = 1,
            Amount = -50.00m,
            Description = "Test transaction",
            Status = TransactionStatus.Cleared // Initial status
        };

        _reconciliationRepository.GetByIdAsync(_reconciliationId, _userId)
            .Returns(reconciliation);
        _reconciliationItemRepository.GetByReconciliationIdAsync(_reconciliationId, _userId)
            .Returns(items);
        _transactionRepository.GetByIdAsync(1, _userId).Returns(transaction);

        var command = new BulkApproveMatchesCommand
        {
            UserId = _userId,
            ReconciliationId = _reconciliationId,
            MinConfidenceThreshold = 0.95m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ApprovedCount.Should().Be(1);
        transaction.Status.Should().Be(TransactionStatus.Reconciled);
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
            // High confidence match
            new ReconciliationItem
            {
                Id = 1,
                ReconciliationId = _reconciliationId,
                TransactionId = 1,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.98m,
                MatchMethod = MatchMethod.Exact,
                BankReferenceData = "{\"externalId\":\"ext1\",\"amount\":-100.00}"
            },
            // Medium confidence match
            new ReconciliationItem
            {
                Id = 2,
                ReconciliationId = _reconciliationId,
                TransactionId = 2,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.92m,
                MatchMethod = MatchMethod.Fuzzy,
                BankReferenceData = "{\"externalId\":\"ext2\",\"amount\":-50.00}"
            },
            // Low confidence match
            new ReconciliationItem
            {
                Id = 3,
                ReconciliationId = _reconciliationId,
                TransactionId = 3,
                ItemType = ReconciliationItemType.Matched,
                MatchConfidence = 0.75m,
                MatchMethod = MatchMethod.Fuzzy,
                BankReferenceData = "{\"externalId\":\"ext3\",\"amount\":-25.00}"
            },
            // Unmatched bank item
            new ReconciliationItem
            {
                Id = 4,
                ReconciliationId = _reconciliationId,
                ItemType = ReconciliationItemType.UnmatchedBank,
                MatchConfidence = null
            },
            // Unmatched system item
            new ReconciliationItem
            {
                Id = 5,
                ReconciliationId = _reconciliationId,
                TransactionId = 4,
                ItemType = ReconciliationItemType.UnmatchedApp,
                MatchConfidence = null
            }
        };
    }

    private List<Transaction> CreateTestTransactions()
    {
        return new List<Transaction>
        {
            new Transaction { Id = 1, AccountId = 1, Amount = -100.00m, Description = "Transaction 1" },
            new Transaction { Id = 2, AccountId = 1, Amount = -50.00m, Description = "Transaction 2" },
            new Transaction { Id = 3, AccountId = 1, Amount = -25.00m, Description = "Transaction 3" },
            new Transaction { Id = 4, AccountId = 1, Amount = -10.00m, Description = "Transaction 4" }
        };
    }
}
