using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Rules.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Application.Services;

public class RulesManagementServiceTests
{
    private readonly ICategorizationRuleRepository _ruleRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<RulesManagementService> _logger;
    private readonly RulesManagementService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public RulesManagementServiceTests()
    {
        _ruleRepository = Substitute.For<ICategorizationRuleRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _logger = Substitute.For<ILogger<RulesManagementService>>();
        _service = new RulesManagementService(_ruleRepository, _transactionRepository, _logger);
    }

    [Fact]
    public async Task ApplyRulesToTransactionAsync_TransactionNotFound_ReturnsNull()
    {
        // Arrange
        var transactionId = 123;
        _transactionRepository.GetByIdAsync(transactionId).Returns((Transaction?)null);

        // Act
        var result = await _service.ApplyRulesToTransactionAsync(transactionId, _userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApplyRulesToTransactionAsync_TransactionBelongsToOtherUser_ReturnsNull()
    {
        // Arrange
        var transactionId = 123;
        var otherUserId = Guid.NewGuid();
        var transaction = CreateTransaction(userId: otherUserId);
        
        _transactionRepository.GetByIdAsync(transactionId).Returns(transaction);

        // Act
        var result = await _service.ApplyRulesToTransactionAsync(transactionId, _userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApplyRulesToTransactionAsync_NoActiveRules_ReturnsNull()
    {
        // Arrange
        var transactionId = 123;
        var transaction = CreateTransaction(userId: _userId);
        
        _transactionRepository.GetByIdAsync(transactionId).Returns(transaction);
        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule>());

        // Act
        var result = await _service.ApplyRulesToTransactionAsync(transactionId, _userId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ApplyRulesToTransactionAsync_RuleMatches_ReturnsMatchResult()
    {
        // Arrange
        var transactionId = 123;
        var categoryId = 456;
        var transaction = CreateTransaction(userId: _userId, description: "WALMART STORE");
        var rule = CreateRule(pattern: "WALMART", categoryId: categoryId);
        
        _transactionRepository.GetByIdAsync(transactionId).Returns(transaction);
        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule> { rule });

        // Act
        var result = await _service.ApplyRulesToTransactionAsync(transactionId, _userId);

        // Assert
        result.Should().NotBeNull();
        result!.RuleId.Should().Be(rule.Id);
        result.CategoryId.Should().Be(categoryId);
        result.ConfidenceScore.Should().BeGreaterThan(0);
        result.MatchedConditions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ApplyRulesToTransactionAsync_MultipleRules_AppliesHighestPriority()
    {
        // Arrange
        var transactionId = 123;
        var transaction = CreateTransaction(userId: _userId, description: "WALMART STORE");
        var highPriorityRule = CreateRule(id: 1, pattern: "WALMART", priority: 1, categoryId: 100);
        var lowPriorityRule = CreateRule(id: 2, pattern: "STORE", priority: 2, categoryId: 200);
        
        _transactionRepository.GetByIdAsync(transactionId).Returns(transaction);
        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule> { lowPriorityRule, highPriorityRule });

        // Act
        var result = await _service.ApplyRulesToTransactionAsync(transactionId, _userId);

        // Assert
        result.Should().NotBeNull();
        result!.RuleId.Should().Be(highPriorityRule.Id);
        result.CategoryId.Should().Be(100); // High priority rule should win
    }

    [Fact]
    public async Task ApplyRulesToTransactionsAsync_ProcessesMultipleTransactions()
    {
        // Arrange
        var transactionIds = new[] { 1, 2, 3 };
        var transactions = new[]
        {
            CreateTransaction(id: 1, userId: _userId, description: "WALMART"),
            CreateTransaction(id: 2, userId: _userId, description: "TARGET"),
            CreateTransaction(id: 3, userId: _userId, description: "AMAZON")
        };
        var rule = CreateRule(pattern: "WALMART");

        _transactionRepository.GetByIdAsync(1).Returns(transactions[0]);
        _transactionRepository.GetByIdAsync(2).Returns(transactions[1]);
        _transactionRepository.GetByIdAsync(3).Returns(transactions[2]);
        _ruleRepository.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationRule> { rule });

        // Act
        var results = await _service.ApplyRulesToTransactionsAsync(transactionIds, _userId);

        // Assert
        results.Should().HaveCount(3);
        results.First(r => r.TransactionId == 1).MatchResult.Should().NotBeNull(); // Should match WALMART
        results.First(r => r.TransactionId == 2).MatchResult.Should().BeNull();    // Should not match TARGET
        results.First(r => r.TransactionId == 3).MatchResult.Should().BeNull();    // Should not match AMAZON
    }

    [Fact]
    public async Task RecordRuleApplicationAsync_ValidRule_CreatesApplication()
    {
        // Arrange
        var ruleId = 1;
        var transactionId = 123;
        var categoryId = 456;
        var rule = CreateRule(id: ruleId);
        
        _ruleRepository.GetRuleByIdAsync(ruleId, _userId, Arg.Any<CancellationToken>())
            .Returns(rule);

        // Act
        var result = await _service.RecordRuleApplicationAsync(ruleId, transactionId, categoryId, _userId);

        // Assert
        result.Should().NotBeNull();
        result.RuleId.Should().Be(ruleId);
        result.TransactionId.Should().Be(transactionId);
        result.CategoryId.Should().Be(categoryId);
        await _ruleRepository.Received(1).UpdateRuleAsync(rule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordRuleApplicationAsync_RuleNotFound_ThrowsException()
    {
        // Arrange
        var ruleId = 999;
        _ruleRepository.GetRuleByIdAsync(ruleId, _userId, Arg.Any<CancellationToken>())
            .Returns((CategorizationRule?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RecordRuleApplicationAsync(ruleId, 123, 456, _userId));
    }

    [Fact]
    public async Task RecordRuleCorrectionAsync_ValidRule_RecordsCorrection()
    {
        // Arrange
        var ruleId = 1;
        var transactionId = 123;
        var newCategoryId = 789;
        var rule = CreateRule(id: ruleId);
        var application = new RuleApplication
        {
            RuleId = ruleId,
            TransactionId = transactionId,
            CategoryId = 456
        };
        rule.Applications.Add(application);

        _ruleRepository.GetRuleByIdAsync(ruleId, _userId, Arg.Any<CancellationToken>())
            .Returns(rule);

        // Act
        await _service.RecordRuleCorrectionAsync(ruleId, transactionId, newCategoryId, _userId);

        // Assert
        application.WasCorrected.Should().BeTrue();
        application.CorrectedCategoryId.Should().Be(newCategoryId);
        rule.CorrectionCount.Should().BeGreaterThan(0);
        await _ruleRepository.Received(1).UpdateRuleAsync(rule, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestRuleAsync_WithMatchingTransactions_ReturnsResults()
    {
        // Arrange
        var rule = CreateRule(pattern: "WALMART");
        var transactions = new[]
        {
            CreateTransaction(description: "WALMART STORE"),
            CreateTransaction(description: "TARGET STORE"),
            CreateTransaction(description: "WALMART GROCERY")
        };

        _transactionRepository.GetRecentTransactionsAsync(_userId, Arg.Any<int>())
            .Returns(transactions);

        // Act
        var result = await _service.TestRuleAsync(rule, _userId);

        // Assert
        result.Should().NotBeNull();
        result.MatchCount.Should().Be(2); // Two WALMART transactions
        result.MatchingTransactions.Should().HaveCount(2);
        result.TotalTransactionsEvaluated.Should().Be(3);
        result.EstimatedAccuracy.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TestRuleAsync_MaxResultsLimit_RespectsLimit()
    {
        // Arrange
        var rule = CreateRule(pattern: "TEST");
        var transactions = Enumerable.Range(1, 100)
            .Select(i => CreateTransaction(id: i, description: "TEST TRANSACTION"))
            .ToArray();

        _transactionRepository.GetRecentTransactionsAsync(_userId, Arg.Any<int>())
            .Returns(transactions);

        // Act
        var result = await _service.TestRuleAsync(rule, _userId, maxResults: 10);

        // Assert
        result.MatchCount.Should().Be(10); // Should respect limit
        result.MatchingTransactions.Should().HaveCount(10);
    }

    [Fact]
    public async Task SuggestRulesAsync_WithCategorizedTransactions_ReturnsSuggestions()
    {
        // Arrange
        var categoryId = 100;
        var category = new Category { Id = categoryId, Name = "Groceries" };
        var transactions = new[]
        {
            CreateTransaction(description: "WALMART GROCERY", categoryId: categoryId, category: category),
            CreateTransaction(description: "WALMART STORE", categoryId: categoryId, category: category),
            CreateTransaction(description: "WALMART SUPERCENTER", categoryId: categoryId, category: category)
        };

        _transactionRepository.GetCategorizedTransactionsAsync(_userId, Arg.Any<int>())
            .Returns(transactions);

        // Act
        var result = await _service.SuggestRulesAsync(_userId);

        // Assert
        result.Should().NotBeEmpty();
        var suggestion = result.First();
        suggestion.CategoryId.Should().Be(categoryId);
        suggestion.Pattern.Should().NotBeEmpty();
        suggestion.Confidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SuggestRulesAsync_InsufficientTransactions_ReturnsEmpty()
    {
        // Arrange - Only 2 transactions (need 3+ for suggestions)
        var transactions = new[]
        {
            CreateTransaction(description: "WALMART", categoryId: 100),
            CreateTransaction(description: "TARGET", categoryId: 200)
        };

        _transactionRepository.GetCategorizedTransactionsAsync(_userId, Arg.Any<int>())
            .Returns(transactions);

        // Act
        var result = await _service.SuggestRulesAsync(_userId);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RuleType.Equals, 0.95)]
    [InlineData(RuleType.StartsWith, 0.85)]
    [InlineData(RuleType.EndsWith, 0.85)]
    [InlineData(RuleType.Contains, 0.75)]
    [InlineData(RuleType.Regex, 0.80)]
    public async Task TestRuleAsync_EstimatedAccuracy_ReflectsRuleType(RuleType ruleType, double expectedAccuracy)
    {
        // Arrange
        var rule = CreateRule(type: ruleType, matchCount: 0); // New rule
        var transactions = new[] { CreateTransaction(description: "TEST") };

        _transactionRepository.GetRecentTransactionsAsync(_userId, Arg.Any<int>())
            .Returns(transactions);

        // Act
        var result = await _service.TestRuleAsync(rule, _userId);

        // Assert
        result.EstimatedAccuracy.Should().BeApproximately(expectedAccuracy, 0.01);
    }

    // Helper methods
    private static CategorizationRule CreateRule(
        int id = 1,
        string pattern = "TEST",
        RuleType type = RuleType.Contains,
        int priority = 0,
        int categoryId = 1,
        int matchCount = 0)
    {
        return new CategorizationRule
        {
            Id = id,
            Name = $"Test Rule {id}",
            Pattern = pattern,
            Type = type,
            Priority = priority,
            IsActive = true,
            CategoryId = categoryId,
            ConfidenceScore = 0.8,
            UserId = Guid.NewGuid(),
            MatchCount = matchCount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Applications = new List<RuleApplication>()
        };
    }

    private static Transaction CreateTransaction(
        int id = 1,
        string description = "Test Transaction",
        decimal amount = 100.0m,
        Guid? userId = null,
        int? categoryId = null,
        Category? category = null)
    {
        var account = new Account
        {
            Id = 1,
            Name = "Test Account",
            Type = AccountType.Checking,
            UserId = userId ?? Guid.NewGuid()
        };

        return new Transaction
        {
            Id = id,
            Description = description,
            Amount = amount,
            Account = account,
            AccountId = account.Id,
            CategoryId = categoryId,
            Category = category,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}