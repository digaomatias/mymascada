using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;
using Xunit;
using FluentAssertions;

namespace MyMascada.Tests.Unit.Services;

public class CategorizationCandidatesServiceTests
{
    private readonly ICategorizationCandidatesRepository _candidatesRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<CategorizationCandidatesService> _logger;
    private readonly CategorizationCandidatesService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public CategorizationCandidatesServiceTests()
    {
        _candidatesRepository = Substitute.For<ICategorizationCandidatesRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _logger = Substitute.For<ILogger<CategorizationCandidatesService>>();
        _service = new CategorizationCandidatesService(_candidatesRepository, _transactionRepository, _logger);
    }

    [Fact]
    public async Task CreateCandidatesAsync_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var candidates = new List<CategorizationCandidate>();

        // Act
        var result = await _service.CreateCandidatesAsync(candidates, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _candidatesRepository.DidNotReceive().GetTransactionIdsWithPendingCandidatesAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCandidatesAsync_AllTransactionsValid_CreatesAllCandidates()
    {
        // Arrange
        var candidates = new List<CategorizationCandidate>
        {
            CreateCandidate(transactionId: 1, categoryId: 123),
            CreateCandidate(transactionId: 2, categoryId: 456),
            CreateCandidate(transactionId: 3, categoryId: 789)
        };

        // No existing pending candidates
        _candidatesRepository.GetPendingCandidatesForTransactionsAsync(
            Arg.Any<IEnumerable<int>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate>());

        _transactionRepository.GetCategorizedTransactionIdsAsync(
            Arg.Any<IEnumerable<int>>())
            .Returns(new HashSet<int>());

        _candidatesRepository.AddCandidatesBatchAsync(
            Arg.Is<IEnumerable<CategorizationCandidate>>(c => c.Count() == 3),
            Arg.Any<CancellationToken>())
            .Returns(candidates);

        // Act
        var result = await _service.CreateCandidatesAsync(candidates, CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        await _candidatesRepository.Received(1).AddCandidatesBatchAsync(
            Arg.Is<IEnumerable<CategorizationCandidate>>(c => c.Count() == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCandidatesAsync_SomeTransactionsHavePendingCandidates_FiltersOutDuplicates()
    {
        // Arrange
        var candidates = new List<CategorizationCandidate>
        {
            CreateCandidate(transactionId: 1, categoryId: 123),
            CreateCandidate(transactionId: 2, categoryId: 456),
            CreateCandidate(transactionId: 3, categoryId: 789)
        };

        // Create an existing candidate for transaction 2 with the same category and method
        var existingCandidate = CreateCandidate(transactionId: 2, categoryId: 456);

        // Mock GetPendingCandidatesForTransactionsAsync to return the existing candidate
        _candidatesRepository.GetPendingCandidatesForTransactionsAsync(
            Arg.Any<IEnumerable<int>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate> { existingCandidate });

        _transactionRepository.GetCategorizedTransactionIdsAsync(
            Arg.Any<IEnumerable<int>>())
            .Returns(new HashSet<int>());

        var validCandidates = candidates.Where(c => c.TransactionId != 2).ToList();
        _candidatesRepository.AddCandidatesBatchAsync(
            Arg.Is<IEnumerable<CategorizationCandidate>>(c => c.Count() == 2),
            Arg.Any<CancellationToken>())
            .Returns(validCandidates);

        // Act
        var result = await _service.CreateCandidatesAsync(candidates, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContain(c => c.TransactionId == 2);
        await _candidatesRepository.Received(1).AddCandidatesBatchAsync(
            Arg.Is<IEnumerable<CategorizationCandidate>>(c => c.Count() == 2 && !c.Any(x => x.TransactionId == 2)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCandidatesAsync_SomeTransactionsAlreadyCategorized_FiltersOutCategorized()
    {
        // Arrange
        var candidates = new List<CategorizationCandidate>
        {
            CreateCandidate(transactionId: 1, categoryId: 123),
            CreateCandidate(transactionId: 2, categoryId: 456),
            CreateCandidate(transactionId: 3, categoryId: 789)
        };

        var categorizedTransactions = new HashSet<int> { 1, 3 }; // Transactions 1 and 3 already categorized

        // No existing pending candidates
        _candidatesRepository.GetPendingCandidatesForTransactionsAsync(
            Arg.Any<IEnumerable<int>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate>());

        _transactionRepository.GetCategorizedTransactionIdsAsync(
            Arg.Any<IEnumerable<int>>())
            .Returns(categorizedTransactions);

        var validCandidates = candidates.Where(c => !categorizedTransactions.Contains(c.TransactionId)).ToList();
        _candidatesRepository.AddCandidatesBatchAsync(
            Arg.Is<IEnumerable<CategorizationCandidate>>(c => c.Count() == 1),
            Arg.Any<CancellationToken>())
            .Returns(validCandidates);

        // Act
        var result = await _service.CreateCandidatesAsync(candidates, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.Should().OnlyContain(c => c.TransactionId == 2);
        await _candidatesRepository.Received(1).AddCandidatesBatchAsync(
            Arg.Is<IEnumerable<CategorizationCandidate>>(c => c.Count() == 1 && c.First().TransactionId == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCandidatesAsync_AllTransactionsFiltered_ReturnsEmptyList()
    {
        // Arrange
        var candidates = new List<CategorizationCandidate>
        {
            CreateCandidate(transactionId: 1, categoryId: 123),
            CreateCandidate(transactionId: 2, categoryId: 456)
        };

        // Create an existing candidate for transaction 1 with the same category and method
        var existingCandidate = CreateCandidate(transactionId: 1, categoryId: 123);
        var categorizedTransactions = new HashSet<int> { 2 }; // Transaction 2 already categorized

        // Mock GetPendingCandidatesForTransactionsAsync to return the existing candidate
        _candidatesRepository.GetPendingCandidatesForTransactionsAsync(
            Arg.Any<IEnumerable<int>>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationCandidate> { existingCandidate });

        _transactionRepository.GetCategorizedTransactionIdsAsync(
            Arg.Any<IEnumerable<int>>())
            .Returns(categorizedTransactions);

        // Act
        var result = await _service.CreateCandidatesAsync(candidates, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _candidatesRepository.DidNotReceive().AddCandidatesBatchAsync(
            Arg.Any<IEnumerable<CategorizationCandidate>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplyCandidateAsync_ValidCandidate_AppliesSuccessfully()
    {
        // Arrange
        var candidateId = 123;
        var transaction = CreateTransaction(1, "TEST TRANSACTION", _userId);
        var candidate = CreateCandidate(transactionId: 1, categoryId: 456);
        candidate.Id = candidateId;
        candidate.Transaction = transaction;
        candidate.Status = "Pending";

        _candidatesRepository.GetByIdAsync(candidateId, Arg.Any<CancellationToken>())
            .Returns(candidate);

        // Act
        var result = await _service.ApplyCandidateAsync(candidateId, "TestUser", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        transaction.CategoryId.Should().Be(456);
        await _transactionRepository.Received(1).UpdateAsync(transaction);
        await _candidatesRepository.Received(1).UpdateCandidateAsync(candidate, Arg.Any<CancellationToken>());
        candidate.Status.Should().Be("Applied");
    }

    [Fact]
    public async Task ApplyCandidateAsync_CandidateNotFound_ReturnsFalse()
    {
        // Arrange
        var candidateId = 123;

        _candidatesRepository.GetByIdAsync(candidateId, Arg.Any<CancellationToken>())
            .Returns((CategorizationCandidate?)null);

        // Act
        var result = await _service.ApplyCandidateAsync(candidateId, "TestUser", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task ApplyCandidateAsync_CandidateNotPending_ReturnsFalse()
    {
        // Arrange
        var candidateId = 123;
        var candidate = CreateCandidate(transactionId: 1, categoryId: 456);
        candidate.Id = candidateId;
        candidate.Status = "Applied"; // Already applied

        _candidatesRepository.GetByIdAsync(candidateId, Arg.Any<CancellationToken>())
            .Returns(candidate);

        // Act
        var result = await _service.ApplyCandidateAsync(candidateId, "TestUser", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task RejectCandidateAsync_ValidCandidate_RejectsSuccessfully()
    {
        // Arrange
        var candidateId = 123;
        var candidate = CreateCandidate(transactionId: 1, categoryId: 456);
        candidate.Id = candidateId;
        candidate.Status = "Pending";

        _candidatesRepository.GetByIdAsync(candidateId, Arg.Any<CancellationToken>())
            .Returns(candidate);

        // Act
        var result = await _service.RejectCandidateAsync(candidateId, "TestUser", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        await _candidatesRepository.Received(1).UpdateCandidateAsync(candidate, Arg.Any<CancellationToken>());
        candidate.Status.Should().Be("Rejected");
    }

    private CategorizationCandidate CreateCandidate(int transactionId, int categoryId)
    {
        return new CategorizationCandidate
        {
            TransactionId = transactionId,
            CategoryId = categoryId,
            CategorizationMethod = "Test",
            ConfidenceScore = 0.8m,
            ProcessedBy = "TestHandler",
            Reasoning = "Test reasoning",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private Transaction CreateTransaction(int id, string description, Guid userId)
    {
        var account = new Account
        {
            Id = 1,
            UserId = userId,
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
}