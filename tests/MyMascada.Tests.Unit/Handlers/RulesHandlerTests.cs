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

public class RulesHandlerTests
{
    private readonly ICategorizationRuleRepository _ruleRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<RulesHandler> _logger;
    private readonly IOptions<CategorizationOptions> _options;
    private readonly RulesHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public RulesHandlerTests()
    {
        _ruleRepository = Substitute.For<ICategorizationRuleRepository>();
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _logger = Substitute.For<ILogger<RulesHandler>>();
        _options = Substitute.For<IOptions<CategorizationOptions>>();
        _options.Value.Returns(new CategorizationOptions());
        _handler = new RulesHandler(_ruleRepository, _categoryRepository, _options, _logger);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_NoTransactions_ReturnsEmptyResult()
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
    public async Task ProcessTransactionsAsync_NoUserIdInTransaction_SkipsProcessingAndReturnsInRemaining()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: null, description: "TEST TRANSACTION")
        };

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
        // Transactions without userId cannot be processed and remain in RemainingTransactions
        result.RemainingTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_NoActiveRules_ReturnsTransactionsInRemaining()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "TEST TRANSACTION")
        };

        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule>());

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
        // When there are no rules, transactions are not processed and go to RemainingTransactions
        result.RemainingTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_HighConfidenceRuleMatch_CreatesAutoAppliedTransaction()
    {
        // Arrange
        var categoryId = 123;
        var categoryName = "Test Category";
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "WALMART STORE")
        };

        var category = CreateCategory(categoryId, categoryName);
        var rule = CreateRule(pattern: "WALMART", categoryId: categoryId, confidence: 0.98m, category: category);

        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule> { rule });
        _categoryRepository.GetByIdAsync(categoryId).Returns(category);

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().HaveCount(1);
        result.Candidates.Should().BeEmpty();

        var autoApplied = result.AutoAppliedTransactions.First();
        autoApplied.CategoryId.Should().Be(categoryId);
        autoApplied.CategoryName.Should().Be(categoryName);
        autoApplied.ConfidenceScore.Should().BeGreaterOrEqualTo(0.95m);
        autoApplied.ProcessedBy.Should().Be("Rules");
    }

    [Fact]
    public async Task ProcessTransactionsAsync_MediumConfidenceRuleMatch_CreatesCandidate()
    {
        // Arrange
        var categoryId = 123;
        var categoryName = "Test Category";
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "TARGET STORE")
        };

        var category = CreateCategory(categoryId, categoryName);
        var rule = CreateRule(pattern: "TARGET", categoryId: categoryId, confidence: 0.8m, category: category);

        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule> { rule });
        _categoryRepository.GetByIdAsync(categoryId).Returns(category);

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().HaveCount(1);

        var candidate = result.Candidates.First();
        candidate.CategoryId.Should().Be(categoryId);
        candidate.ConfidenceScore.Should().BeLessThan(0.95m);
        candidate.CategorizationMethod.Should().Be("Rule");
        candidate.Status.Should().Be("Pending");
        candidate.ProcessedBy.Should().Be("RulesHandler");
    }

    [Fact]
    public async Task ProcessTransactionsAsync_NoRuleMatches_ReturnsTransactionsInRemaining()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "UNKNOWN MERCHANT")
        };

        var rule = CreateRule(pattern: "WALMART", categoryId: 123);

        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule> { rule });

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
        // When no rules match, transactions are not processed and go to RemainingTransactions
        result.RemainingTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_MultipleTransactions_ProcessesAll()
    {
        // Arrange - Use unique IDs for each transaction
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "WALMART STORE", id: 1),
            CreateTransaction(userId: _userId, description: "TARGET STORE", id: 2),
            CreateTransaction(userId: _userId, description: "UNKNOWN MERCHANT", id: 3)
        };

        var walmartCategory = CreateCategory(123, "Walmart Category");
        var targetCategory = CreateCategory(456, "Target Category");
        var walmartRule = CreateRule(pattern: "WALMART", categoryId: 123, confidence: 0.98m, category: walmartCategory);
        var targetRule = CreateRule(pattern: "TARGET", categoryId: 456, confidence: 0.85m, category: targetCategory);

        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule> { walmartRule, targetRule });
        _categoryRepository.GetByIdAsync(123).Returns(walmartCategory);
        _categoryRepository.GetByIdAsync(456).Returns(targetCategory);

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().HaveCount(1); // Walmart (high confidence)
        result.Candidates.Should().HaveCount(1); // Target (medium confidence)
        result.RemainingTransactions.Should().HaveCount(1); // Unknown merchant (no match)
        result.Metrics.ProcessedByRules.Should().Be(2);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_RuleWithNonexistentCategory_SkipsRule()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "WALMART STORE")
        };

        // Create rule without Category set to simulate missing/deleted category
        var rule = CreateRule(pattern: "WALMART", categoryId: 999, setCategory: false);

        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule> { rule });
        _categoryRepository.GetByIdAsync(999).Returns((Category?)null);

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
    }

    private Transaction CreateTransaction(Guid? userId, string description, int id = 1, int accountId = 1)
    {
        var account = new Account
        {
            Id = accountId,
            UserId = userId ?? Guid.NewGuid(),
            Name = "Test Account",
            Type = AccountType.Checking
        };

        return new Transaction
        {
            Id = id,
            Description = description,
            Amount = -100.00m,
            TransactionDate = DateTime.Now.AddDays(-1),
            AccountId = accountId,
            Account = account
        };
    }

    private CategorizationRule CreateRule(string pattern, int categoryId, decimal? confidence = null, Category? category = null, bool setCategory = true)
    {
        var rule = new CategorizationRule
        {
            Id = Random.Shared.Next(1, 1000),
            Name = $"Test Rule for {pattern}",
            Pattern = pattern,
            Type = RuleType.Contains,
            CategoryId = categoryId,
            ConfidenceScore = confidence.HasValue ? (double?)confidence.Value : null,
            Priority = 1,
            IsActive = true,
            UserId = _userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Set the Category navigation property (required for RulesHandler to get category name)
        if (setCategory)
        {
            rule.Category = category ?? CreateCategory(categoryId, $"Category {categoryId}");
        }

        return rule;
    }

    private Category CreateCategory(int id, string name)
    {
        return new Category
        {
            Id = id,
            Name = name,
            Type = CategoryType.Expense,
            Color = "#FF0000",
            UserId = _userId
        };
    }
}