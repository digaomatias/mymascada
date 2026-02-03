using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Commands;

public class BulkCategorizeWithLlmCommandTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISharedCategorizationService _sharedCategorizationService;
    private readonly ICategorizationCandidatesService _candidatesService;
    private readonly ILogger<BulkCategorizeWithLlmCommandHandler> _logger;
    private readonly BulkCategorizeWithLlmCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public BulkCategorizeWithLlmCommandTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _sharedCategorizationService = Substitute.For<ISharedCategorizationService>();
        _candidatesService = Substitute.For<ICategorizationCandidatesService>();
        _logger = Substitute.For<ILogger<BulkCategorizeWithLlmCommandHandler>>();
        
        _handler = new BulkCategorizeWithLlmCommandHandler(
            _transactionRepository,
            _sharedCategorizationService,
            _candidatesService,
            _logger);
    }

    [Fact]
    public async Task Handle_EmptyTransactionIds_ReturnsSuccessWithNoProcessing()
    {
        // Arrange
        var command = new BulkCategorizeWithLlmCommand
        {
            UserId = _userId,
            TransactionIds = new List<int>(),
            MaxBatchSize = 50
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalTransactions.Should().Be(0);
        result.ProcessedTransactions.Should().Be(0);
        result.CandidatesCreated.Should().Be(0);
        result.Success.Should().BeTrue();
        result.Message.Should().Be("No valid transactions found for the provided IDs");
    }

    [Fact]
    public async Task Handle_NoTransactionsFoundForUser_ReturnsSuccessWithNoProcessing()
    {
        // Arrange
        var command = new BulkCategorizeWithLlmCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1, 2, 3 },
            MaxBatchSize = 50
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(command.TransactionIds)),
            _userId,
            Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalTransactions.Should().Be(3);
        result.ProcessedTransactions.Should().Be(0);
        result.CandidatesCreated.Should().Be(0);
        result.Success.Should().BeTrue();
        result.Message.Should().Be("No valid transactions found for the provided IDs");
    }

    [Fact]
    public async Task Handle_ValidTransactions_ProcessesSuccessfully()
    {
        // Arrange
        var command = new BulkCategorizeWithLlmCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1, 2 },
            MaxBatchSize = 50
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(1, "WALMART STORE"),
            CreateTransaction(2, "STARBUCKS COFFEE")
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
                        new() { CategoryId = 123, CategoryName = "Shopping", Confidence = 0.85m }
                    }
                },
                new()
                {
                    TransactionId = 2,
                    Suggestions = new List<CategorySuggestion>
                    {
                        new() { CategoryId = 456, CategoryName = "Coffee", Confidence = 0.75m }
                    }
                }
            }
        };

        var candidates = new List<CategorizationCandidate>
        {
            CreateCandidate(1, 123, 0.85m),
            CreateCandidate(2, 456, 0.75m)
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Is<IEnumerable<int>>(ids => ids.SequenceEqual(command.TransactionIds)),
            _userId,
            Arg.Any<CancellationToken>())
            .Returns(transactions);

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Is<IEnumerable<Transaction>>(t => t.Count() == 2),
            _userId,
            Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        _sharedCategorizationService.ConvertToCategorizationCandidates(
            llmResponse,
            $"BulkLLM-{_userId}")
            .Returns(candidates);

        _candidatesService.CreateCandidatesAsync(
            Arg.Is<IEnumerable<CategorizationCandidate>>(c => c.Count() == 2),
            Arg.Any<CancellationToken>())
            .Returns(candidates);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalTransactions.Should().Be(2);
        result.ProcessedTransactions.Should().Be(2);
        result.CandidatesCreated.Should().Be(2);
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Successfully processed 2 transactions and created 2 candidates");
    }

    [Fact]
    public async Task Handle_LargeBatch_ProcessesInMultipleBatches()
    {
        // Arrange
        var command = new BulkCategorizeWithLlmCommand
        {
            UserId = _userId,
            TransactionIds = Enumerable.Range(1, 5).ToList(), // 5 transactions
            MaxBatchSize = 2 // Force multiple batches
        };

        var transactions = command.TransactionIds.Select(id => CreateTransaction(id, $"MERCHANT {id}")).ToList();

        var llmResponse1 = new LlmCategorizationResponse
        {
            Success = true,
            Categorizations = new List<TransactionCategorization>
            {
                new() { TransactionId = 1, Suggestions = new List<CategorySuggestion> { new() { CategoryId = 100, CategoryName = "Category1", Confidence = 0.8m } } },
                new() { TransactionId = 2, Suggestions = new List<CategorySuggestion> { new() { CategoryId = 200, CategoryName = "Category2", Confidence = 0.8m } } }
            }
        };

        var llmResponse2 = new LlmCategorizationResponse
        {
            Success = true,
            Categorizations = new List<TransactionCategorization>
            {
                new() { TransactionId = 3, Suggestions = new List<CategorySuggestion> { new() { CategoryId = 300, CategoryName = "Category3", Confidence = 0.8m } } },
                new() { TransactionId = 4, Suggestions = new List<CategorySuggestion> { new() { CategoryId = 400, CategoryName = "Category4", Confidence = 0.8m } } }
            }
        };

        var llmResponse3 = new LlmCategorizationResponse
        {
            Success = true,
            Categorizations = new List<TransactionCategorization>
            {
                new() { TransactionId = 5, Suggestions = new List<CategorySuggestion> { new() { CategoryId = 500, CategoryName = "Category5", Confidence = 0.8m } } }
            }
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Is<IEnumerable<Transaction>>(t => t.Count() == 2),
            _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse1, llmResponse2);

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Is<IEnumerable<Transaction>>(t => t.Count() == 1),
            _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse3);

        _sharedCategorizationService.ConvertToCategorizationCandidates(Arg.Any<LlmCategorizationResponse>(), Arg.Any<string>())
            .Returns(new List<CategorizationCandidate> { CreateCandidate(1, 100, 0.8m) });

        _candidatesService.CreateCandidatesAsync(Arg.Any<IEnumerable<CategorizationCandidate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate> { CreateCandidate(1, 100, 0.8m) });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalTransactions.Should().Be(5);
        result.ProcessedTransactions.Should().Be(5);
        result.Success.Should().BeTrue();

        // Verify that LLM service was called 3 times (3 batches: 2+2+1)
        await _sharedCategorizationService.Received(3).GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LLMServiceFails_ReturnsPartialFailure()
    {
        // Arrange
        var command = new BulkCategorizeWithLlmCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1, 2 },
            MaxBatchSize = 2
        };

        var transactions = new List<Transaction>
        {
            CreateTransaction(1, "WALMART STORE"),
            CreateTransaction(2, "STARBUCKS COFFEE")
        };

        var llmResponse = new LlmCategorizationResponse
        {
            Success = false,
            Errors = new List<string> { "LLM service unavailable" }
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalTransactions.Should().Be(2);
        result.ProcessedTransactions.Should().Be(0);
        result.CandidatesCreated.Should().Be(0);
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("LLM service unavailable");
    }

    [Fact]
    public async Task Handle_ExceptionThrown_ReturnsFailureResult()
    {
        // Arrange
        var command = new BulkCategorizeWithLlmCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1 },
            MaxBatchSize = 50
        };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IEnumerable<Transaction>>(new Exception("Database error")));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalTransactions.Should().Be(1);
        result.ProcessedTransactions.Should().Be(0);
        result.CandidatesCreated.Should().Be(0);
        result.Success.Should().BeFalse();
        result.Errors.Should().Contain("Critical error: Database error");
        result.Message.Should().Be("Bulk categorization failed due to a critical error");
    }

    [Fact]
    public async Task Handle_CandidateCreationFails_ContinuesProcessing()
    {
        // Arrange
        var command = new BulkCategorizeWithLlmCommand
        {
            UserId = _userId,
            TransactionIds = new List<int> { 1 },
            MaxBatchSize = 50
        };

        var transactions = new List<Transaction> { CreateTransaction(1, "WALMART STORE") };

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
                        new() { CategoryId = 123, CategoryName = "Shopping", Confidence = 0.85m }
                    }
                }
            }
        };

        var candidates = new List<CategorizationCandidate> { CreateCandidate(1, 123, 0.85m) };

        _transactionRepository.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        _sharedCategorizationService.GetCategorizationSuggestionsAsync(
            Arg.Any<IEnumerable<Transaction>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(llmResponse);

        _sharedCategorizationService.ConvertToCategorizationCandidates(Arg.Any<LlmCategorizationResponse>(), Arg.Any<string>())
            .Returns(candidates);

        _candidatesService.CreateCandidatesAsync(Arg.Any<IEnumerable<CategorizationCandidate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate>()); // Returns empty list (filtered out)

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalTransactions.Should().Be(1);
        result.ProcessedTransactions.Should().Be(1);
        result.CandidatesCreated.Should().Be(0); // No candidates were actually created
        result.Success.Should().BeTrue();
    }

    private Transaction CreateTransaction(int id, string description)
    {
        var account = new Account
        {
            Id = 1,
            UserId = _userId,
            Name = "Test Account",
            Type = AccountType.Checking
        };

        return new Transaction
        {
            Id = id,
            Description = description,
            Amount = -100.00m,
            TransactionDate = DateTime.Now.AddDays(-1),
            AccountId = 1,
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
            ProcessedBy = $"BulkLLM-{_userId}",
            Reasoning = "LLM bulk analysis",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}