using Microsoft.EntityFrameworkCore;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Repositories;
using Xunit;

namespace MyMascada.Tests.Unit.Repositories;

public class CategorizationCandidatesRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CategorizationCandidatesRepository _repository;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public CategorizationCandidatesRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new CategorizationCandidatesRepository(_context);

        // Seed test data
        SeedTestData().Wait();
    }

    private async Task SeedTestData()
    {
        // Create test users
        var user = new User
        {
            Id = _userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            FirstName = "Test",
            LastName = "User"
        };

        var otherUser = new User
        {
            Id = _otherUserId,
            Email = "other@example.com",
            PasswordHash = "hash",
            FirstName = "Other",
            LastName = "User"
        };

        _context.Users.AddRange(user, otherUser);

        // Create test accounts
        var account1 = new Account
        {
            Id = 1,
            UserId = _userId,
            Name = "Test Checking",
            Type = AccountType.Checking,
            CurrentBalance = 1000m
        };

        var account2 = new Account
        {
            Id = 2,
            UserId = _otherUserId,
            Name = "Other Checking",
            Type = AccountType.Checking,
            CurrentBalance = 2000m
        };

        _context.Accounts.AddRange(account1, account2);

        // Create test categories
        var category1 = new Category
        {
            Id = 1,
            Name = "Groceries",
            Color = "#FF0000",
            UserId = _userId
        };

        var category2 = new Category
        {
            Id = 2,
            Name = "Dining",
            Color = "#00FF00",
            UserId = _userId
        };

        _context.Categories.AddRange(category1, category2);

        // Create test transactions
        var transaction1 = new Transaction
        {
            Id = 1,
            Amount = -100.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-5),
            Description = "Walmart",
            AccountId = 1,
            CategoryId = null // Uncategorized
        };

        var transaction2 = new Transaction
        {
            Id = 2,
            Amount = -50.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-3),
            Description = "Starbucks",
            AccountId = 1,
            CategoryId = null // Uncategorized
        };

        var transaction3 = new Transaction
        {
            Id = 3,
            Amount = -75.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-1),
            Description = "Restaurant",
            AccountId = 1,
            CategoryId = 2 // Already categorized
        };

        var transaction4 = new Transaction
        {
            Id = 4,
            Amount = -200.00m,
            TransactionDate = DateTime.UtcNow.AddDays(-2),
            Description = "Other User Transaction",
            AccountId = 2,
            CategoryId = null
        };

        _context.Transactions.AddRange(transaction1, transaction2, transaction3, transaction4);

        // Create test candidates
        var candidate1 = new CategorizationCandidate
        {
            Id = 1,
            TransactionId = 1,
            CategoryId = 1,
            CategorizationMethod = "Rule",
            ConfidenceScore = 0.9m,
            ProcessedBy = "RulesHandler",
            Reasoning = "Pattern match",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow.AddHours(-2)
        };

        var candidate2 = new CategorizationCandidate
        {
            Id = 2,
            TransactionId = 2,
            CategoryId = 2,
            CategorizationMethod = "LLM",
            ConfidenceScore = 0.75m,
            ProcessedBy = "LLMHandler",
            Reasoning = "AI analysis",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var candidate3 = new CategorizationCandidate
        {
            Id = 3,
            TransactionId = 1,
            CategoryId = 2,
            CategorizationMethod = "ML",
            ConfidenceScore = 0.6m,
            ProcessedBy = "MLHandler",
            Reasoning = "Machine learning",
            Status = "Rejected",
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        _context.CategorizationCandidates.AddRange(candidate1, candidate2, candidate3);

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetTransactionIdsWithPendingCandidatesAsync_WithEmptyList_ReturnsEmpty()
    {
        // Arrange
        var transactionIds = new List<int>();

        // Act
        var result = await _repository.GetTransactionIdsWithPendingCandidatesAsync(transactionIds, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTransactionIdsWithPendingCandidatesAsync_WithPendingCandidates_ReturnsCorrectIds()
    {
        // Arrange
        var transactionIds = new[] { 1, 2, 3, 4 };

        // Act
        var result = await _repository.GetTransactionIdsWithPendingCandidatesAsync(transactionIds, CancellationToken.None);

        // Assert
        // Transactions 1 and 2 have pending candidates
        // Transaction 3 is already categorized
        // Transaction 4 belongs to another user
        var expectedIds = new HashSet<int> { 1, 2 };
        Assert.Equal(expectedIds, result);
        Assert.Contains(1, result);
        Assert.Contains(2, result);
        Assert.DoesNotContain(3, result);
        Assert.DoesNotContain(4, result);
    }

    [Fact]
    public async Task GetTransactionIdsWithPendingCandidatesAsync_OnlyRejectedCandidates_ReturnsEmpty()
    {
        // Arrange
        // Add a transaction with only rejected candidates
        var transaction = new Transaction
        {
            Id = 5,
            Amount = -30.00m,
            TransactionDate = DateTime.UtcNow,
            Description = "Test",
            AccountId = 1,
            CategoryId = null
        };
        _context.Transactions.Add(transaction);

        var rejectedCandidate = new CategorizationCandidate
        {
            Id = 4,
            TransactionId = 5,
            CategoryId = 1,
            CategorizationMethod = "Rule",
            ConfidenceScore = 0.8m,
            ProcessedBy = "RulesHandler",
            Reasoning = "Test",
            Status = "Rejected",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.CategorizationCandidates.Add(rejectedCandidate);
        await _context.SaveChangesAsync();

        var transactionIds = new[] { 5 };

        // Act
        var result = await _repository.GetTransactionIdsWithPendingCandidatesAsync(transactionIds, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTransactionIdsWithPendingCandidatesAsync_OnlyAppliedCandidates_ReturnsEmpty()
    {
        // Arrange
        // Add a transaction with only applied candidates
        var transaction = new Transaction
        {
            Id = 6,
            Amount = -40.00m,
            TransactionDate = DateTime.UtcNow,
            Description = "Test Applied",
            AccountId = 1,
            CategoryId = null
        };
        _context.Transactions.Add(transaction);

        var appliedCandidate = new CategorizationCandidate
        {
            Id = 5,
            TransactionId = 6,
            CategoryId = 1,
            CategorizationMethod = "Rule",
            ConfidenceScore = 0.95m,
            ProcessedBy = "RulesHandler",
            Reasoning = "High confidence match",
            Status = "Applied",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.CategorizationCandidates.Add(appliedCandidate);
        await _context.SaveChangesAsync();

        var transactionIds = new[] { 6 };

        // Act
        var result = await _repository.GetTransactionIdsWithPendingCandidatesAsync(transactionIds, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTransactionIdsWithPendingCandidatesAsync_WithNonexistentIds_IgnoresMissing()
    {
        // Arrange
        var transactionIds = new[] { 1, 2, 999, 1000 }; // 999 and 1000 don't exist

        // Act
        var result = await _repository.GetTransactionIdsWithPendingCandidatesAsync(transactionIds, CancellationToken.None);

        // Assert
        // Should only return existing transactions with pending candidates
        var expectedIds = new HashSet<int> { 1, 2 };
        Assert.Equal(expectedIds, result);
        Assert.DoesNotContain(999, result);
        Assert.DoesNotContain(1000, result);
    }

    [Fact]
    public async Task GetPendingCandidatesForTransactionAsync_ValidTransaction_ReturnsPendingOnly()
    {
        // Arrange
        var transactionId = 1; // Has both pending and rejected candidates

        // Act
        var result = await _repository.GetPendingCandidatesForTransactionAsync(transactionId, CancellationToken.None);
        var candidatesList = result.ToList();

        // Assert
        Assert.Single(candidatesList); // Should only return the pending candidate
        Assert.All(candidatesList, c => Assert.Equal("Pending", c.Status));
        Assert.All(candidatesList, c => Assert.Equal(transactionId, c.TransactionId));
    }

    [Fact]
    public async Task GetPendingCandidatesForTransactionAsync_NoTransaction_ReturnsEmpty()
    {
        // Arrange
        var transactionId = 999; // Doesn't exist

        // Act
        var result = await _repository.GetPendingCandidatesForTransactionAsync(transactionId, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}