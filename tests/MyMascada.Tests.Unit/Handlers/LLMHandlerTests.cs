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
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<LLMHandler> _logger;
    private readonly LLMHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public LLMHandlerTests()
    {
        _sharedCategorizationService = Substitute.For<ISharedCategorizationService>();
        _subscriptionService = Substitute.For<ISubscriptionService>();
        _logger = Substitute.For<ILogger<LLMHandler>>();

        // Default: SelfHosted tier (unlimited) so existing tests pass unchanged
        _subscriptionService.CanUseLlmCategorizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new AiFeatureAccessResult(true, SubscriptionTier.SelfHosted, RemainingQuota: int.MaxValue));

        _handler = new LLMHandler(_sharedCategorizationService, _subscriptionService, _logger);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_NoTransactions_ReturnsEmptyResult()
    {
        var transactions = new List<Transaction>();

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
        result.RemainingTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessTransactionsAsync_NoUserIdInTransaction_SkipsProcessingAndReturnsInRemaining()
    {
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: null, description: "TEST TRANSACTION")
        };

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
        result.RemainingTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_LLMServiceFails_ReturnsErrorResult()
    {
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

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.Errors.Should().Contain("LLM service unavailable");
        result.RemainingTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_LLMServiceSucceeds_CreatesCandidates()
    {
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

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty(); // LLM never auto-applies
        result.Candidates.Should().HaveCount(2);
        result.Metrics.ProcessedByLLM.Should().Be(2);
        result.RemainingTransactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_ValidTransactions_UpdatesMetrics()
    {
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

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Should().NotBeNull();
        result.Metrics.ProcessedByLLM.Should().Be(2);
        result.Metrics.EstimatedCostSavings.Should().BeGreaterThan(0);
        result.Metrics.CategoryDistribution.Should().NotBeEmpty();
        result.Metrics.ConfidenceDistribution.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessTransactionsAsync_ExceptionThrown_ReturnsErrorResult()
    {
        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "TEST TRANSACTION")
        };

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<LlmCategorizationResponse>(new Exception("Network error")));

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.Errors.Should().Contain("LLM processing failed: Network error");
        result.RemainingTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessTransactionsAsync_EmptyLLMResponse_ReturnsEmptyResult()
    {
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

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.Metrics.ProcessedByLLM.Should().Be(0);
        result.RemainingTransactions.Should().HaveCount(1);
    }

    // --- Phase 3: Tier gating tests ---

    [Fact]
    public async Task ProcessTransactionsAsync_FreeUser_SkipsProcessing()
    {
        _subscriptionService.CanUseLlmCategorizationAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new AiFeatureAccessResult(false, SubscriptionTier.Free, "Free plan"));

        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "TEST TRANSACTION")
        };

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Should().NotBeNull();
        result.Candidates.Should().BeEmpty();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Errors.Should().BeEmpty();

        // LLM service should never be called for free users
        await _sharedCategorizationService.DidNotReceive()
            .GetCategorizationSuggestionsAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTransactionsAsync_ProUserWithQuota_ProcessesNormally()
    {
        _subscriptionService.CanUseLlmCategorizationAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new AiFeatureAccessResult(true, SubscriptionTier.Pro, RemainingQuota: 100));

        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "UNKNOWN MERCHANT", id: 1)
        };

        var llmResponse = new LlmCategorizationResponse
        {
            Success = true,
            Categorizations = new List<TransactionCategorization>()
        };

        var candidates = new List<CategorizationCandidate>
        {
            CreateCandidate(transactionId: 1, categoryId: 123, confidence: 0.85m)
        };

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse);
        _sharedCategorizationService.ConvertToCategorizationCandidates(
            llmResponse, $"LLMHandler-{_userId}")
            .Returns(candidates);

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Candidates.Should().HaveCount(1);
        result.Metrics.ProcessedByLLM.Should().Be(1);

        // Usage should be recorded
        await _subscriptionService.Received(1)
            .RecordLlmUsageAsync(_userId, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTransactionsAsync_ProUserQuotaExceeded_SkipsWithWarning()
    {
        _subscriptionService.CanUseLlmCategorizationAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new AiFeatureAccessResult(false, SubscriptionTier.Pro, "quota exceeded"));

        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "TEST TRANSACTION")
        };

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Candidates.Should().BeEmpty();
        result.Errors.Should().ContainSingle()
            .Which.Should().Contain("quota exceeded");

        await _sharedCategorizationService.DidNotReceive()
            .GetCategorizationSuggestionsAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTransactionsAsync_ProUserPartialQuota_CapsBatchSize()
    {
        _subscriptionService.CanUseLlmCategorizationAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new AiFeatureAccessResult(true, SubscriptionTier.Pro, RemainingQuota: 2));

        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "TX 1", id: 1),
            CreateTransaction(userId: _userId, description: "TX 2", id: 2),
            CreateTransaction(userId: _userId, description: "TX 3", id: 3),
            CreateTransaction(userId: _userId, description: "TX 4", id: 4),
            CreateTransaction(userId: _userId, description: "TX 5", id: 5)
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
            llmResponse, Arg.Any<string>())
            .Returns(candidates);

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        // Verify only 2 transactions were sent to LLM (quota cap)
        await _sharedCategorizationService.Received(1)
            .GetCategorizationSuggestionsAsync(
                Arg.Is<IEnumerable<Transaction>>(t => t.Count() == 2),
                _userId,
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessTransactionsAsync_SelfHostedUser_ProcessesUnlimited()
    {
        // Default mock already returns SelfHosted with unlimited quota — no override needed

        var transactions = new List<Transaction>
        {
            CreateTransaction(userId: _userId, description: "MERCHANT A", id: 1),
            CreateTransaction(userId: _userId, description: "MERCHANT B", id: 2)
        };

        var llmResponse = new LlmCategorizationResponse
        {
            Success = true,
            Categorizations = new List<TransactionCategorization>()
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

        var result = await _handler.HandleAsync(transactions, CancellationToken.None);

        result.Candidates.Should().HaveCount(2);
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
                Color = "#FF0000"
            }
        };
    }
}
