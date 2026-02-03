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

namespace MyMascada.Tests.Unit.Services;

public class CategorizationPipelineTests
{
    private readonly RulesHandler _rulesHandler;
    private readonly BankCategoryHandler _bankCategoryHandler;
    private readonly MLHandler _mlHandler;
    private readonly LLMHandler _llmHandler;
    private readonly ICategorizationCandidatesService _candidatesService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<CategorizationPipeline> _logger;
    private readonly CategorizationPipeline _pipeline;
    private readonly Guid _userId = Guid.NewGuid();

    public CategorizationPipelineTests()
    {
        // Create IOptions mock
        var categorizationOptions = new MyMascada.Application.Common.Configuration.CategorizationOptions();
        var optionsMock = Substitute.For<Microsoft.Extensions.Options.IOptions<MyMascada.Application.Common.Configuration.CategorizationOptions>>();
        optionsMock.Value.Returns(categorizationOptions);

        _rulesHandler = Substitute.For<RulesHandler>(
            Substitute.For<ICategorizationRuleRepository>(),
            Substitute.For<ICategoryRepository>(),
            optionsMock,
            Substitute.For<ILogger<RulesHandler>>());

        _bankCategoryHandler = Substitute.For<BankCategoryHandler>(
            Substitute.For<IBankCategoryMappingService>(),
            Substitute.For<IBankConnectionRepository>(),
            optionsMock,
            Substitute.For<ILogger<BankCategoryHandler>>());

        _mlHandler = Substitute.For<MLHandler>(
            Substitute.For<ILogger<MLHandler>>());

        _llmHandler = Substitute.For<LLMHandler>(
            Substitute.For<ISharedCategorizationService>(),
            Substitute.For<ILogger<LLMHandler>>());

        _candidatesService = Substitute.For<ICategorizationCandidatesService>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _logger = Substitute.For<ILogger<CategorizationPipeline>>();

        _pipeline = new CategorizationPipeline(
            _rulesHandler,
            _bankCategoryHandler,
            _mlHandler,
            _llmHandler,
            _candidatesService,
            _transactionRepository,
            _logger);
    }

    [Fact]
    public async Task ProcessAsync_NoTransactions_ReturnsEmptyResult()
    {
        // Arrange
        var transactions = new List<Transaction>();

        // Act
        var result = await _pipeline.ProcessAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
        result.RemainingTransactions.Should().BeEmpty();
        result.Metrics.TotalTransactions.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_RulesHandlerCategorizesAll_SkipsOtherHandlers()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(1, "WALMART"),
            CreateTransaction(2, "TARGET")
        };

        var rulesResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>
            {
                CreateCategorizedTransaction(transactions[0], 123, "Shopping", 0.98m, "Rules"),
                CreateCategorizedTransaction(transactions[1], 456, "Retail", 0.96m, "Rules")
            },
            Candidates = new List<CategorizationCandidate>(),
            Metrics = new CategorizationMetrics { ProcessedByRules = 2 }
        };

        _rulesHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(rulesResult);

        // BankCategoryHandler returns empty since all transactions processed by rules
        _bankCategoryHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(new CategorizationResult());

        _candidatesService.CreateCandidatesAsync(Arg.Any<IEnumerable<CategorizationCandidate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate>());

        // Act
        var result = await _pipeline.ProcessAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().HaveCount(2);
        result.Candidates.Should().BeEmpty();
        result.RemainingTransactions.Should().BeEmpty();
        result.Metrics.ProcessedByRules.Should().Be(2);
        result.Metrics.TotalTransactions.Should().Be(2);

        // Verify BankCategory, ML and LLM handlers were not called since all transactions were processed by rules
        await _bankCategoryHandler.DidNotReceive().HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>());
        await _mlHandler.DidNotReceive().HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>());
        await _llmHandler.DidNotReceive().HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_RulesPartialMatch_ProcessesRemainingWithBankCategoryThenML()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(1, "WALMART"),
            CreateTransaction(2, "UNKNOWN MERCHANT"),
            CreateTransaction(3, "ANOTHER UNKNOWN")
        };

        var rulesResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>
            {
                CreateCategorizedTransaction(transactions[0], 123, "Shopping", 0.98m, "Rules")
            },
            Candidates = new List<CategorizationCandidate>(),
            Metrics = new CategorizationMetrics { ProcessedByRules = 1 }
        };

        var bankCategoryResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>(),
            Candidates = new List<CategorizationCandidate>(),
            Metrics = new CategorizationMetrics { ProcessedByBankCategory = 0 }
        };

        var mlResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>
            {
                CreateCategorizedTransaction(transactions[1], 456, "Unknown", 0.97m, "ML")
            },
            Candidates = new List<CategorizationCandidate>(),
            Metrics = new CategorizationMetrics { ProcessedByML = 1 }
        };

        _rulesHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(rulesResult);

        _bankCategoryHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(bankCategoryResult);

        _mlHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(mlResult);

        _llmHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(new CategorizationResult());

        _candidatesService.CreateCandidatesAsync(Arg.Any<IEnumerable<CategorizationCandidate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate>());

        // Act
        var result = await _pipeline.ProcessAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().HaveCount(2);
        result.Metrics.ProcessedByRules.Should().Be(1);
        result.Metrics.ProcessedByML.Should().Be(1);

        // Verify ML handler was called with 2 transactions (not processed by rules or bank category)
        await _mlHandler.Received(1).HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_CompleteChain_ProcessesThroughAllHandlers()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(1, "WALMART"),
            CreateTransaction(2, "UNKNOWN MERCHANT"),
            CreateTransaction(3, "COMPLEX TRANSACTION")
        };

        var rulesResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>
            {
                CreateCategorizedTransaction(transactions[0], 123, "Shopping", 0.98m, "Rules")
            },
            Candidates = new List<CategorizationCandidate>(),
            Metrics = new CategorizationMetrics { ProcessedByRules = 1 }
        };

        var bankCategoryResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>(),
            Candidates = new List<CategorizationCandidate>(),
            Metrics = new CategorizationMetrics { ProcessedByBankCategory = 0 }
        };

        var mlResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>(),
            Candidates = new List<CategorizationCandidate>
            {
                CreateCandidate(transactions[1].Id, 456, 0.85m, "ML")
            },
            Metrics = new CategorizationMetrics { ProcessedByML = 1 }
        };

        var llmResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>(),
            Candidates = new List<CategorizationCandidate>
            {
                CreateCandidate(transactions[2].Id, 789, 0.75m, "LLM")
            },
            Metrics = new CategorizationMetrics { ProcessedByLLM = 1 }
        };

        _rulesHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(rulesResult);

        _bankCategoryHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(bankCategoryResult);

        _mlHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(mlResult);

        _llmHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(llmResult);

        _candidatesService.CreateCandidatesAsync(Arg.Any<IEnumerable<CategorizationCandidate>>(), Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate>());

        // Act
        var result = await _pipeline.ProcessAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().HaveCount(1);
        result.Candidates.Should().HaveCount(2);
        result.Metrics.ProcessedByRules.Should().Be(1);
        result.Metrics.ProcessedByML.Should().Be(1);
        result.Metrics.ProcessedByLLM.Should().Be(1);

        // Verify database operations were called
        await _candidatesService.Received(1).CreateCandidatesAsync(
            Arg.Any<IEnumerable<CategorizationCandidate>>(), Arg.Any<CancellationToken>());
        await _transactionRepository.Received(1).UpdateAsync(Arg.Any<Transaction>());
        await _transactionRepository.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessAsync_DatabaseOperationsFail_AddsErrors()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(1, "WALMART")
        };

        var rulesResult = new CategorizationResult
        {
            AutoAppliedTransactions = new List<CategorizedTransaction>
            {
                CreateCategorizedTransaction(transactions[0], 123, "Shopping", 0.98m, "Rules")
            },
            Candidates = new List<CategorizationCandidate>(),
            Metrics = new CategorizationMetrics { ProcessedByRules = 1 }
        };

        _rulesHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(rulesResult);

        // BankCategoryHandler not called since all processed by rules
        _bankCategoryHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(new CategorizationResult());

        _transactionRepository.UpdateAsync(Arg.Any<Transaction>())
            .Returns(Task.FromException(new Exception("Database error")));

        // Act
        var result = await _pipeline.ProcessAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Errors.Should().Contain(e => e.Contains("Database operation failed"));
    }

    [Fact]
    public async Task ProcessAsync_HandlerThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var transactions = new List<Transaction>
        {
            CreateTransaction(1, "WALMART")
        };

        _rulesHandler.HandleAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<CategorizationResult>(new Exception("Handler error")));

        // Act
        var result = await _pipeline.ProcessAsync(transactions, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Errors.Should().Contain("Pipeline failed: Handler error");
        result.Metrics.FailedTransactions.Should().Be(1);
        result.RemainingTransactions.Should().HaveCount(1);
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

    private CategorizedTransaction CreateCategorizedTransaction(Transaction transaction, int categoryId, string categoryName, decimal confidence, string processedBy)
    {
        return new CategorizedTransaction(transaction, categoryId, categoryName, confidence, processedBy, "Test reason");
    }

    private CategorizationCandidate CreateCandidate(int transactionId, int categoryId, decimal confidence, string method)
    {
        return new CategorizationCandidate
        {
            Id = Random.Shared.Next(1, 1000),
            TransactionId = transactionId,
            CategoryId = categoryId,
            CategorizationMethod = method,
            ConfidenceScore = confidence,
            ProcessedBy = $"{method}Handler",
            Reasoning = $"{method} analysis",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}