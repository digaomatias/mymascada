using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Handlers;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Handlers;

public class LLMHandlerTests
{
    private readonly ISharedCategorizationService _sharedCategorizationService;
    private readonly ILogger<LLMHandler> _logger;
    private readonly LLMHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public LLMHandlerTests()
    {
        _sharedCategorizationService = Substitute.For<ISharedCategorizationService>();
        _logger = Substitute.For<ILogger<LLMHandler>>();
        _handler = new LLMHandler(_sharedCategorizationService, _logger);
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
    public async Task ProcessTransactionsAsync_LLMServiceFails_ReturnsErrorResult()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "UNKNOWN MERCHANT")
        };

        var llmResponse = new LlmCategorizationResponse
        {
            Success = false,
            Errors = new List<string> { "LLM service unavailable" }
        };

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.Errors.Should().Contain("LLM service unavailable");
        result.RemainingTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_LLMServiceSucceeds_CreatesCandidates()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "UNKNOWN MERCHANT", id: 1),
            CreateTransaction(userId: _userId, description: "ANOTHER MERCHANT", id: 2)
        };

        var llmResponse = new LlmCategorizationResponse
        {
            Success = true,
            Categorizations = new List<TransactionCategorization>
            {
                new()
                {
                    TransactionId = 1,
                    Suggestions = new List<CategorySuggestion>
                    {
                        new() { CategoryId = 123, CategoryName = "Shopping", Confidence = 0.85m, Reasoning = "Merchant analysis" }
                    }
                },
                new()
                {
                    TransactionId = 2,
                    Suggestions = new List<CategorySuggestion>
                    {
                        new() { CategoryId = 456, CategoryName = "Dining", Confidence = 0.75m, Reasoning = "Food merchant" }
                    }
                }
            }
        };

        var candidates = new List<CategorizationCandidate>
        {
            CreateCandidate(transactionId: 1, categoryId: 123, confidence: 0.85m),
            CreateCandidate(transactionId: 2, categoryId: 456, confidence: 0.75m)
        };

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        _sharedCategorizationService.ConvertToCategorizationCandidates(
            llmResponse, $"LLMHandler-{_userId}")
            .Returns(candidates);

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty(); // LLM never auto-applies
        result.Candidates.Should().HaveCount(2);
        result.Metrics.ProcessedByLLM.Should().Be(2);
        result.RemainingTransactions.Should().HaveCount(2); // Transactions remain for final processing
    }

    [Fact]
    public async Task ProcessTransactionsAsync_ValidTransactions_UpdatesMetrics()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "MERCHANT A"),
            CreateTransaction(userId: _userId, description: "MERCHANT B")
        };

        var llmResponse = new LlmCategorizationResponse
        {
            Success = true,
            Categorizations = new List<TransactionCategorization>()
        };

        var candidates = new List<CategorizationCandidate>
        {
            CreateCandidate(transactionId: 1, categoryId: 123, confidence: 0.8m),
            CreateCandidate(transactionId: 2, categoryId: 456, confidence: 0.7m)
        };

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        _sharedCategorizationService.ConvertToCategorizationCandidates(
            llmResponse, $"LLMHandler-{_userId}")
            .Returns(candidates);

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Metrics.ProcessedByLLM.Should().Be(2);
        result.Metrics.EstimatedCostSavings.Should().BeGreaterThan(0);
        result.Metrics.CategoryDistribution.Should().NotBeEmpty();
        result.Metrics.ConfidenceDistribution.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessTransactionsAsync_ExceptionThrown_ReturnsErrorResult()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "TEST TRANSACTION")
        };

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LlmCategorizationResponse>(new Exception("Network error")));

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.Errors.Should().Contain("LLM processing failed: Network error");
        result.RemainingTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_EmptyLLMResponse_ReturnsEmptyResult()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "TEST TRANSACTION")
        };

        var llmResponse = new LlmCategorizationResponse
        {
            Success = true,
            Categorizations = new List<TransactionCategorization>()
        };

        var candidates = new List<CategorizationCandidate>();

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        _sharedCategorizationService.ConvertToCategorizationCandidates(
            llmResponse, $"LLMHandler-{_userId}")
            .Returns(candidates);

        // Act
        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.Metrics.ProcessedByLLM.Should().Be(0);
        result.RemainingTransactions.Should().HaveCount(1);
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

    private CategorizationCandidate CreateCandidate(int transactionId, int categoryId, decimal confidence)
    {
        return new CategorizationCandidate
        {
            Id = Random.Shared.Next(1, 1000),
            TransactionId = transactionId,
            CategoryId = categoryId,
            CategorizationMethod = "LLM",
            ConfidenceScore = confidence,
            ProcessedBy = $"LLMHandler-{_userId}",
            Reasoning = "LLM analysis",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Category = new Category
            {
                Id = categoryId,
                Name = $"Category {categoryId}",
                Type = CategoryType.Expense,
                Color = "#FF0000"
            }
        };
    }
}