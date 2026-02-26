using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Handlers;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Handlers;

public class BankCategoryHandlerTests
{
    private readonly IBankCategoryMappingService _bankCategoryMappingService;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly ILogger<BankCategoryHandler> _logger;
    private readonly IOptions<CategorizationOptions> _options;
    private readonly BankCategoryHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public BankCategoryHandlerTests()
    {
        _bankCategoryMappingService = Substitute.For<IBankCategoryMappingService>();
        _bankConnectionRepository = Substitute.For<IBankConnectionRepository>();
        _logger = Substitute.For<ILogger<BankCategoryHandler>>();
        _options = Substitute.For<IOptions<CategorizationOptions>>();
        _options.Value.Returns(new CategorizationOptions { AutoApplyConfidenceThreshold = 0.9m });
        _handler = new BankCategoryHandler(_bankCategoryMappingService, _bankConnectionRepository, _options, _logger);
    }

    [Fact]
    public async Task HandleAsync_NoTransactions_ReturnsEmptyResult()
    {
        // Arrange
        var transactions = new List<Transaction>();

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
        result.RemainingTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_TransactionsWithoutBankCategory_ReturnsEmptyResult()
    {
        // Arrange: Transaction without bank category
        var transactions = new List<Transaction>
        {
            CreateTransaction(id: 1, description: "TEST TRANSACTION", bankCategory: null)
        };

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenMappingIsExcluded_ShouldSkipAndNotCategorize()
    {
        // Arrange: Transaction with excluded bank category mapping
        // Business Scenario: User has excluded "Lending Services" category because it incorrectly
        // categorizes loan payments as generic "Financial Services" instead of letting ML/LLM
        // categorize them more specifically.
        var bankCategory = "Lending Services";
        var categoryId = 100;
        var transactions = new List<Transaction>
        {
            CreateTransaction(id: 1, description: "LOAN PAYMENT", bankCategory: bankCategory)
        };

        var excludedMappingResult = new BankCategoryMappingResult
        {
            Mapping = CreateMapping(id: 1, bankCategory: bankCategory, categoryId: categoryId, isExcluded: true),
            CategoryId = categoryId,
            CategoryName = "Financial Services",
            ConfidenceScore = 0.95m,
            IsExcluded = true, // Key: this mapping is excluded
            WasExactMatch = false,
            WasCreatedByAI = true
        };

        _bankConnectionRepository.GetByAccountIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((BankConnection?)null);

        _bankCategoryMappingService.ResolveAndCreateMappingsAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<string>(),
            _userId,
            Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, BankCategoryMappingResult>
            {
                { bankCategory, excludedMappingResult }
            });

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert: Transaction not categorized, not added to AutoApplied or Candidates
        result.AutoAppliedTransactions.Should().BeEmpty("excluded mappings should not auto-apply");
        result.Candidates.Should().BeEmpty("excluded mappings should not create candidates");
        result.CategorizedTransactions.Should().BeEmpty("excluded mappings should not categorize transactions");

        // Verify mapping application was NOT recorded since transaction was skipped
        await _bankCategoryMappingService.DidNotReceive()
            .RecordMappingApplicationAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenMappingIsNotExcluded_ShouldCategorizeNormally()
    {
        // Arrange: Transaction with active (not excluded) bank category mapping
        // Business Scenario: "Supermarket and groceries" bank category correctly maps to "Groceries"
        var bankCategory = "Supermarket and groceries";
        var categoryId = 200;
        var categoryName = "Groceries";
        var transactions = new List<Transaction>
        {
            CreateTransaction(id: 1, description: "COUNTDOWN STORE", bankCategory: bankCategory)
        };

        var activeMappingResult = new BankCategoryMappingResult
        {
            Mapping = CreateMapping(id: 2, bankCategory: bankCategory, categoryId: categoryId, isExcluded: false),
            CategoryId = categoryId,
            CategoryName = categoryName,
            ConfidenceScore = 0.95m,
            IsExcluded = false, // Key: this mapping is NOT excluded
            WasExactMatch = true,
            WasCreatedByAI = false
        };

        _bankConnectionRepository.GetByAccountIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((BankConnection?)null);

        _bankCategoryMappingService.ResolveAndCreateMappingsAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<string>(),
            _userId,
            Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, BankCategoryMappingResult>
            {
                { bankCategory, activeMappingResult }
            });

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert: Transaction is auto-applied with high confidence
        result.AutoAppliedTransactions.Should().HaveCount(1);
        result.AutoAppliedTransactions.First().CategoryId.Should().Be(categoryId);
        result.AutoAppliedTransactions.First().CategoryName.Should().Be(categoryName);
        result.Candidates.Should().BeEmpty("high confidence should result in auto-apply, not candidate");

        // Verify mapping application was recorded
        await _bankCategoryMappingService.Received(1)
            .RecordMappingApplicationAsync(2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MixedExcludedAndActiveTransactions_ProcessesCorrectly()
    {
        // Arrange: Multiple transactions - some with excluded mapping, some with active mapping
        // Business Scenario: Batch of transactions where some bank categories are excluded
        var excludedBankCategory = "Lending Services";
        var activeBankCategory = "Supermarket and groceries";
        var categoryId = 200;
        var categoryName = "Groceries";

        var transactions = new List<Transaction>
        {
            CreateTransaction(id: 1, description: "LOAN PAYMENT", bankCategory: excludedBankCategory),
            CreateTransaction(id: 2, description: "COUNTDOWN STORE", bankCategory: activeBankCategory),
            CreateTransaction(id: 3, description: "ANOTHER LOAN", bankCategory: excludedBankCategory)
        };

        var excludedMappingResult = new BankCategoryMappingResult
        {
            Mapping = CreateMapping(id: 1, bankCategory: excludedBankCategory, categoryId: 100, isExcluded: true),
            CategoryId = 100,
            CategoryName = "Financial Services",
            ConfidenceScore = 0.95m,
            IsExcluded = true
        };

        var activeMappingResult = new BankCategoryMappingResult
        {
            Mapping = CreateMapping(id: 2, bankCategory: activeBankCategory, categoryId: categoryId, isExcluded: false),
            CategoryId = categoryId,
            CategoryName = categoryName,
            ConfidenceScore = 0.95m,
            IsExcluded = false
        };

        _bankConnectionRepository.GetByAccountIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((BankConnection?)null);

        _bankCategoryMappingService.ResolveAndCreateMappingsAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<string>(),
            _userId,
            Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, BankCategoryMappingResult>
            {
                { excludedBankCategory, excludedMappingResult },
                { activeBankCategory, activeMappingResult }
            });

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert: Only the active mapping should be applied
        result.AutoAppliedTransactions.Should().HaveCount(1, "only non-excluded mappings should be applied");
        result.AutoAppliedTransactions.First().Transaction.Id.Should().Be(2, "only transaction with active mapping should be categorized");
        result.AutoAppliedTransactions.First().CategoryName.Should().Be(categoryName);

        // Excluded transactions should NOT be categorized
        result.AutoAppliedTransactions.Should().NotContain(t => t.Transaction.Id == 1);
        result.AutoAppliedTransactions.Should().NotContain(t => t.Transaction.Id == 3);
    }

    [Fact]
    public async Task HandleAsync_LowConfidenceWithActiveMapping_CreatesCandidate()
    {
        // Arrange: Active mapping but with low confidence (below auto-apply threshold)
        var bankCategory = "Online Shopping";
        var categoryId = 300;
        var categoryName = "Shopping";
        var transactions = new List<Transaction>
        {
            CreateTransaction(id: 1, description: "AMAZON PURCHASE", bankCategory: bankCategory)
        };

        var lowConfidenceMappingResult = new BankCategoryMappingResult
        {
            Mapping = CreateMapping(id: 3, bankCategory: bankCategory, categoryId: categoryId, isExcluded: false),
            CategoryId = categoryId,
            CategoryName = categoryName,
            ConfidenceScore = 0.75m, // Below the 0.9 threshold
            IsExcluded = false,
            WasExactMatch = false,
            WasCreatedByAI = true
        };

        _bankConnectionRepository.GetByAccountIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((BankConnection?)null);

        _bankCategoryMappingService.ResolveAndCreateMappingsAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<string>(),
            _userId,
            Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, BankCategoryMappingResult>
            {
                { bankCategory, lowConfidenceMappingResult }
            });

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert: Creates candidate instead of auto-applying
        result.AutoAppliedTransactions.Should().BeEmpty("low confidence should not auto-apply");
        result.Candidates.Should().HaveCount(1);
        result.Candidates.First().CategoryId.Should().Be(categoryId);
        result.Candidates.First().ConfidenceScore.Should().Be(0.75m);
        result.Candidates.First().CategorizationMethod.Should().Be(CandidateMethod.BankCategory);
    }

    [Fact]
    public async Task HandleAsync_ExcludedMappingWithLowConfidence_ShouldStillBeSkipped()
    {
        // Arrange: Excluded mapping regardless of confidence score
        // Business Scenario: Exclusion flag should take precedence over confidence
        var bankCategory = "Lending Services";
        var transactions = new List<Transaction>
        {
            CreateTransaction(id: 1, description: "LOAN PAYMENT", bankCategory: bankCategory)
        };

        var excludedMappingResult = new BankCategoryMappingResult
        {
            Mapping = CreateMapping(id: 4, bankCategory: bankCategory, categoryId: 100, isExcluded: true),
            CategoryId = 100,
            CategoryName = "Financial Services",
            ConfidenceScore = 0.75m, // Low confidence but still excluded
            IsExcluded = true
        };

        _bankConnectionRepository.GetByAccountIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((BankConnection?)null);

        _bankCategoryMappingService.ResolveAndCreateMappingsAsync(
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<string>(),
            _userId,
            Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, BankCategoryMappingResult>
            {
                { bankCategory, excludedMappingResult }
            });

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert: Excluded means no categorization at all, regardless of confidence
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty("excluded mappings should not create candidates even with low confidence");
    }

    private Transaction CreateTransaction(int id, string description, string? bankCategory, int accountId = 1)
    {
        var account = new Account
        {
            Id = accountId,
            UserId = _userId,
            Name = "Test Account",
            Type = AccountType.Checking
        };

        return new Transaction
        {
            Id = id,
            Description = description,
            BankCategory = bankCategory,
            Amount = -100.00m,
            TransactionDate = DateTime.Now.AddDays(-1),
            AccountId = accountId,
            Account = account
        };
    }

    private BankCategoryMapping CreateMapping(int id, string bankCategory, int categoryId, bool isExcluded)
    {
        return new BankCategoryMapping
        {
            Id = id,
            BankCategoryName = bankCategory,
            NormalizedName = bankCategory.ToLowerInvariant(),
            ProviderId = "akahu",
            UserId = _userId,
            CategoryId = categoryId,
            ConfidenceScore = 0.95m,
            Source = "AI",
            IsActive = true,
            IsExcluded = isExcluded,
            Category = new Category
            {
                Id = categoryId,
                Name = "Test Category",
                UserId = _userId
            }
        };
    }
}
