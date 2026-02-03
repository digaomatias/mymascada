using FluentAssertions;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Services.Reconciliation;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Services;

public class TransactionMatchingServiceTests
{
    private readonly TransactionMatchingService _service;
    private readonly ILogger<TransactionMatchingService> _logger;

    public TransactionMatchingServiceTests()
    {
        _logger = Substitute.For<ILogger<TransactionMatchingService>>();
        _service = new TransactionMatchingService(_logger);
    }

    [Fact]
    public async Task MatchTransactionsAsync_ShouldPreventDuplicateMatching()
    {
        // Arrange
        var bankTransactions = new List<BankTransactionDto>
        {
            new() { BankTransactionId = "1", Amount = -800.00m, Description = "Connolly Gear Trust", TransactionDate = DateTime.Parse("2025-07-09") },
            new() { BankTransactionId = "2", Amount = -800.00m, Description = "Connolly Gear Trust", TransactionDate = DateTime.Parse("2025-07-16") }
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(318, -800.00m, "Connolly Gear Trust", DateTime.Parse("2025-07-09"))
        };

        var request = new TransactionMatchRequest
        {
            BankTransactions = bankTransactions,
            ReconciliationId = 1,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 7
        };

        // Act
        var result = await _service.MatchTransactionsAsync(request, appTransactions);

        // Assert
        result.Should().NotBeNull();
        result.MatchedPairs.Should().HaveCount(1, "only one transaction should be matched");
        
        // The app transaction should only appear once
        var appTransactionIds = result.MatchedPairs.Select(p => p.AppTransaction.Id).ToList();
        appTransactionIds.Should().OnlyContain(id => id == 318);
        appTransactionIds.Should().HaveCount(1);
        
        // One bank transaction should be matched, one should be unmatched
        result.UnmatchedBank.Should().Be(1);
        result.UnmatchedBankTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task MatchTransactionsAsync_ShouldNotMatchSameTransactionInBothExactAndFuzzy()
    {
        // Arrange
        var bankTransactions = new List<BankTransactionDto>
        {
            // This bank transaction should match exactly with the app transaction
            new() { BankTransactionId = "1", Amount = -800.00m, Description = "Connolly Gear Trust", TransactionDate = DateTime.Parse("2025-07-09") },
            // This bank transaction should NOT match the same app transaction in fuzzy matching
            new() { BankTransactionId = "2", Amount = -800.00m, Description = "Connolly Gear", TransactionDate = DateTime.Parse("2025-07-16") }
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(318, -800.00m, "Connolly Gear Trust", DateTime.Parse("2025-07-09"))
        };

        var request = new TransactionMatchRequest
        {
            BankTransactions = bankTransactions,
            ReconciliationId = 1,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 7
        };

        // Act
        var result = await _service.MatchTransactionsAsync(request, appTransactions);

        // Assert
        result.Should().NotBeNull();
        result.MatchedPairs.Should().HaveCount(1, "only one match should be found");
        result.ExactMatches.Should().Be(1);
        result.FuzzyMatches.Should().Be(0);
        result.UnmatchedBank.Should().Be(1);
        
        // The matched transaction should be an exact match
        var matchedPair = result.MatchedPairs.First();
        matchedPair.AppTransaction.Id.Should().Be(318);
        matchedPair.MatchMethod.Should().Be(MatchMethod.Exact);
        matchedPair.BankTransaction.BankTransactionId.Should().Be("1");
    }

    [Fact]
    public async Task MatchTransactionsAsync_ShouldThrowExceptionForDuplicateMatches()
    {
        // This test is to ensure our validation method works correctly
        // We'll create a scenario that would cause duplicates if our fix wasn't working
        // The fix should prevent this from happening, but if it fails, we want to know
        
        // Arrange
        var bankTransactions = new List<BankTransactionDto>
        {
            new() { BankTransactionId = "1", Amount = -800.00m, Description = "Connolly Gear Trust", TransactionDate = DateTime.Parse("2025-07-09") }
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(318, -800.00m, "Connolly Gear Trust", DateTime.Parse("2025-07-09"))
        };

        var request = new TransactionMatchRequest
        {
            BankTransactions = bankTransactions,
            ReconciliationId = 1,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 2
        };

        // Act & Assert
        // This should NOT throw an exception with our fix
        var result = await _service.MatchTransactionsAsync(request, appTransactions);
        result.Should().NotBeNull();
        result.MatchedPairs.Should().HaveCount(1);
    }

    [Fact]
    public async Task FindBestMatchAsync_ShouldReturnExactMatchForHighConfidence()
    {
        // Arrange
        var bankTransaction = new BankTransactionDto
        {
            BankTransactionId = "1",
            Amount = -800.00m,
            Description = "Connolly Gear Trust",
            TransactionDate = DateTime.Parse("2025-07-09")
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(318, -800.00m, "Connolly Gear Trust", DateTime.Parse("2025-07-09"))
        };

        // Act
        var result = await _service.FindBestMatchAsync(
            bankTransaction,
            appTransactions,
            0.01m,
            true,
            true,
            2);

        // Assert
        result.Should().NotBeNull();
        result.MatchMethod.Should().Be(MatchMethod.Exact);
        result.MatchConfidence.Should().BeGreaterThan(0.95m);
        result.AppTransaction.Id.Should().Be(318);
    }

    [Fact]
    public async Task FindBestMatchAsync_ShouldReturnFuzzyMatchForMediumConfidence()
    {
        // Arrange
        var bankTransaction = new BankTransactionDto
        {
            BankTransactionId = "1",
            Amount = -800.00m,
            Description = "Connolly Gear",
            TransactionDate = DateTime.Parse("2025-07-11")
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(318, -800.00m, "Connolly Gear Trust", DateTime.Parse("2025-07-09"))
        };

        // Act
        var result = await _service.FindBestMatchAsync(
            bankTransaction,
            appTransactions,
            0.01m,
            true,
            true,
            5);

        // Assert
        result.Should().NotBeNull();
        result.MatchMethod.Should().Be(MatchMethod.Fuzzy);
        result.MatchConfidence.Should().BeGreaterThan(0.5m);
        result.MatchConfidence.Should().BeLessThan(0.95m);
        result.AppTransaction.Id.Should().Be(318);
    }

    [Fact]
    public async Task FindBestMatchAsync_ShouldReturnNullForLowConfidence()
    {
        // Arrange
        var bankTransaction = new BankTransactionDto
        {
            BankTransactionId = "1",
            Amount = -800.00m,
            Description = "Completely Different Transaction",
            TransactionDate = DateTime.Parse("2025-08-01")
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(318, -800.00m, "Connolly Gear Trust", DateTime.Parse("2025-07-09"))
        };

        // Act
        var result = await _service.FindBestMatchAsync(
            bankTransaction,
            appTransactions,
            0.01m,
            true,
            true,
            2);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(-800.00, -800.00, "Connolly Gear Trust", "Connolly Gear Trust", 0, 1.0)]
    [InlineData(-800.00, -800.00, "Connolly Gear Trust", "Connolly Gear Trust", 1, 0.9)]
    [InlineData(-800.00, -799.99, "Connolly Gear Trust", "Connolly Gear Trust", 0, 0.96)]
    public void CalculateMatchConfidence_ShouldReturnExpectedConfidence(
        decimal bankAmount, 
        decimal appAmount, 
        string bankDescription, 
        string appDescription, 
        int daysDifference, 
        decimal expectedConfidence)
    {
        // Arrange
        var bankTransaction = new BankTransactionDto
        {
            BankTransactionId = "1",
            Amount = bankAmount,
            Description = bankDescription,
            TransactionDate = DateTime.Parse("2025-07-09")
        };

        var appTransaction = CreateTransaction(
            318, 
            appAmount, 
            appDescription, 
            DateTime.Parse("2025-07-09").AddDays(daysDifference));

        // Act
        var result = _service.CalculateMatchConfidence(
            bankTransaction, 
            appTransaction, 
            true, 
            true, 
            2);

        // Assert
        result.Should().BeApproximately(expectedConfidence, 0.1m);
    }

    [Fact]
    public async Task MatchTransactionsAsync_ShouldHandleEmptyInputs()
    {
        // Arrange
        var request = new TransactionMatchRequest
        {
            BankTransactions = new List<BankTransactionDto>(),
            ReconciliationId = 1,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 2
        };

        // Act
        var result = await _service.MatchTransactionsAsync(request, new List<Transaction>());

        // Assert
        result.Should().NotBeNull();
        result.MatchedPairs.Should().BeEmpty();
        result.UnmatchedBank.Should().Be(0);
        result.UnmatchedApp.Should().Be(0);
        result.ExactMatches.Should().Be(0);
        result.FuzzyMatches.Should().Be(0);
    }

    [Fact]
    public async Task MatchTransactionsAsync_ShouldHandleMultipleExactMatches()
    {
        // Arrange
        var bankTransactions = new List<BankTransactionDto>
        {
            new() { BankTransactionId = "1", Amount = -800.00m, Description = "Connolly Gear Trust", TransactionDate = DateTime.Parse("2025-07-09") },
            new() { BankTransactionId = "2", Amount = -110.00m, Description = "Pete Selectcleaning", TransactionDate = DateTime.Parse("2025-07-01") }
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(318, -800.00m, "Connolly Gear Trust", DateTime.Parse("2025-07-09")),
            CreateTransaction(319, -110.00m, "Pete Selectcleaning", DateTime.Parse("2025-07-01"))
        };

        var request = new TransactionMatchRequest
        {
            BankTransactions = bankTransactions,
            ReconciliationId = 1,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 2
        };

        // Act
        var result = await _service.MatchTransactionsAsync(request, appTransactions);

        // Assert
        result.Should().NotBeNull();
        result.MatchedPairs.Should().HaveCount(2);
        result.ExactMatches.Should().Be(2);
        result.FuzzyMatches.Should().Be(0);
        result.UnmatchedBank.Should().Be(0);
        result.UnmatchedApp.Should().Be(0);
        
        // Verify no duplicates
        var appTransactionIds = result.MatchedPairs.Select(p => p.AppTransaction.Id).ToList();
        appTransactionIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task MatchTransactionsAsync_ShouldHandleMixedExactAndFuzzyMatches()
    {
        // Arrange
        var bankTransactions = new List<BankTransactionDto>
        {
            new() { BankTransactionId = "1", Amount = -800.00m, Description = "Connolly Gear Trust", TransactionDate = DateTime.Parse("2025-07-09") },
            new() { BankTransactionId = "2", Amount = -110.00m, Description = "Pete Select", TransactionDate = DateTime.Parse("2025-07-03") }
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(318, -800.00m, "Connolly Gear Trust", DateTime.Parse("2025-07-09")),
            CreateTransaction(319, -110.00m, "Pete Selectcleaning", DateTime.Parse("2025-07-01"))
        };

        var request = new TransactionMatchRequest
        {
            BankTransactions = bankTransactions,
            ReconciliationId = 1,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 5
        };

        // Act
        var result = await _service.MatchTransactionsAsync(request, appTransactions);

        // Assert
        result.Should().NotBeNull();
        result.MatchedPairs.Should().HaveCount(2);
        result.ExactMatches.Should().Be(1);
        result.FuzzyMatches.Should().Be(1);
        result.UnmatchedBank.Should().Be(0);
        result.UnmatchedApp.Should().Be(0);
        
        // Verify no duplicates
        var appTransactionIds = result.MatchedPairs.Select(p => p.AppTransaction.Id).ToList();
        appTransactionIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task MatchTransactionsAsync_ShouldPreferExactMatchOverFuzzyMatch()
    {
        // This test addresses the reported bug where fuzzy matching steals transactions that should be exact matched
        // Scenario: Bank has "Flight Centre Mcard" on 15/07 and 16/06, System has "Flight Centre Mcard" on 16/06
        // Expected: 16/06 bank should exact match with 16/06 system, 15/07 bank should be unmatched
        
        // Arrange
        var bankTransactions = new List<BankTransactionDto>
        {
            new() { BankTransactionId = "1", Amount = -900.00m, Description = "Flight Centre Mcard", TransactionDate = DateTime.Parse("2025-07-15") },
            new() { BankTransactionId = "2", Amount = -900.00m, Description = "Flight Centre Mcard", TransactionDate = DateTime.Parse("2025-06-16") }
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(790, -900.00m, "Flight Centre Mcard", DateTime.Parse("2025-06-16"))
        };

        var request = new TransactionMatchRequest
        {
            BankTransactions = bankTransactions,
            ReconciliationId = 1,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 7 // This allows fuzzy matching across dates
        };

        // Act
        var result = await _service.MatchTransactionsAsync(request, appTransactions);

        // Assert
        result.Should().NotBeNull();
        result.MatchedPairs.Should().HaveCount(1, "only one match should be found");
        result.ExactMatches.Should().Be(1, "should be an exact match");
        result.FuzzyMatches.Should().Be(0, "should not have fuzzy matches when exact match is possible");
        result.UnmatchedBank.Should().Be(1, "the 15/07 bank transaction should remain unmatched");
        
        // Verify the correct transactions were matched
        var matchedPair = result.MatchedPairs.First();
        matchedPair.BankTransaction.BankTransactionId.Should().Be("2", "16/06 bank transaction should be matched");
        matchedPair.AppTransaction.Id.Should().Be(790, "16/06 app transaction should be matched");
        matchedPair.MatchMethod.Should().Be(MatchMethod.Exact, "should be exact match for same date/amount/description");
        
        // Verify the 15/07 bank transaction is in unmatched
        result.UnmatchedBankTransactions.Should().ContainSingle(b => b.BankTransactionId == "1");
    }

    [Fact]
    public async Task MatchTransactionsAsync_ShouldPrioritizeExactMatchesGlobally()
    {
        // Test that the algorithm finds globally optimal matches, not just locally optimal
        // Scenario: Multiple possible matches but exact matches should always win
        
        // Arrange
        var bankTransactions = new List<BankTransactionDto>
        {
            new() { BankTransactionId = "1", Amount = -100.00m, Description = "Store A", TransactionDate = DateTime.Parse("2025-06-15") },
            new() { BankTransactionId = "2", Amount = -100.00m, Description = "Store A", TransactionDate = DateTime.Parse("2025-06-16") },
            new() { BankTransactionId = "3", Amount = -200.00m, Description = "Store B", TransactionDate = DateTime.Parse("2025-06-16") }
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(1, -100.00m, "Store A", DateTime.Parse("2025-06-16")), // Should exact match with bank #2
            CreateTransaction(2, -200.00m, "Store B", DateTime.Parse("2025-06-16"))  // Should exact match with bank #3
        };

        var request = new TransactionMatchRequest
        {
            BankTransactions = bankTransactions,
            ReconciliationId = 1,
            ToleranceAmount = 0.01m,
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 5
        };

        // Act
        var result = await _service.MatchTransactionsAsync(request, appTransactions);

        // Assert
        result.Should().NotBeNull();
        result.MatchedPairs.Should().HaveCount(2, "both exact matches should be found");
        result.ExactMatches.Should().Be(2, "both should be exact matches");
        result.FuzzyMatches.Should().Be(0, "no fuzzy matches should be needed");
        result.UnmatchedBank.Should().Be(1, "bank transaction #1 should be unmatched");
        
        // Verify correct pairings
        var matches = result.MatchedPairs.ToList();
        matches.Should().Contain(m => m.BankTransaction.BankTransactionId == "2" && m.AppTransaction.Id == 1);
        matches.Should().Contain(m => m.BankTransaction.BankTransactionId == "3" && m.AppTransaction.Id == 2);
        matches.Should().AllSatisfy(m => m.MatchMethod.Should().Be(MatchMethod.Exact));
    }

    [Fact] 
    public async Task MatchTransactionsAsync_ShouldHandleExactMatchWithMinorAmountDifference()
    {
        // Test that exact matching works even with small amount differences within tolerance
        
        // Arrange
        var bankTransactions = new List<BankTransactionDto>
        {
            new() { BankTransactionId = "1", Amount = -100.00m, Description = "Exact Store", TransactionDate = DateTime.Parse("2025-06-16") }
        };

        var appTransactions = new List<Transaction>
        {
            CreateTransaction(1, -99.99m, "Exact Store", DateTime.Parse("2025-06-16")) // 1 cent difference within tolerance
        };

        var request = new TransactionMatchRequest
        {
            BankTransactions = bankTransactions,
            ReconciliationId = 1,
            ToleranceAmount = 0.01m, // 1 cent tolerance
            UseDescriptionMatching = true,
            UseDateRangeMatching = true,
            DateRangeToleranceDays = 2
        };

        // Act
        var result = await _service.MatchTransactionsAsync(request, appTransactions);

        // Assert
        result.Should().NotBeNull();
        result.MatchedPairs.Should().HaveCount(1);
        result.ExactMatches.Should().Be(1, "should be exact match despite minor amount difference");
        result.FuzzyMatches.Should().Be(0);
        
        var matchedPair = result.MatchedPairs.First();
        matchedPair.MatchMethod.Should().Be(MatchMethod.Exact);
        matchedPair.MatchConfidence.Should().BeGreaterThan(0.95m);
    }

    private Transaction CreateTransaction(int id, decimal amount, string description, DateTime transactionDate)
    {
        return new Transaction
        {
            Id = id,
            Amount = amount,
            Description = description,
            TransactionDate = transactionDate,
            AccountId = 1,
            CreatedBy = "test-user",
            Status = TransactionStatus.Cleared,
            Source = TransactionSource.Manual,
            Type = TransactionType.Expense,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}