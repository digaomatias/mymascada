using MyMascada.Domain.Enums;
using FluentAssertions;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Services;
using MyMascada.Domain.Entities;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Reconciliation.Services;

public class MatchConfidenceCalculatorTests
{
    private readonly MatchConfidenceCalculator _calculator;

    public MatchConfidenceCalculatorTests()
    {
        _calculator = new MatchConfidenceCalculator();
    }

    [Fact]
    public void CalculateMatchConfidence_WithExactMatch_ReturnsHighConfidence()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-100.50m, "Grocery Store Purchase", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-100.50m, "Grocery Store Purchase", DateTime.UtcNow);

        // Act
        var confidence = _calculator.CalculateMatchConfidence(systemTransaction, bankTransaction);

        // Assert
        confidence.Should().BeGreaterThan(0.95m);
    }

    [Fact]
    public void CalculateMatchConfidence_WithSmallAmountDifference_ReturnsGoodConfidence()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-100.50m, "Grocery Store", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-100.51m, "Grocery Store Purchase", DateTime.UtcNow);

        // Act
        var confidence = _calculator.CalculateMatchConfidence(systemTransaction, bankTransaction);

        // Assert
        confidence.Should().BeGreaterThan(0.85m);
    }

    [Fact]
    public void CalculateMatchConfidence_WithLargeAmountDifference_ReturnsLowConfidence()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-100.00m, "Store Purchase", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-150.00m, "Store Purchase", DateTime.UtcNow);

        // Act
        var confidence = _calculator.CalculateMatchConfidence(systemTransaction, bankTransaction);

        // Assert
        confidence.Should().BeLessThan(0.70m);
    }

    [Fact]
    public void CalculateMatchConfidence_WithDateDifference_AdjustsConfidence()
    {
        // Arrange
        var baseDate = DateTime.UtcNow;
        var systemTransaction = CreateSystemTransaction(-100.00m, "Store Purchase", baseDate);
        var bankTransaction = CreateBankTransaction(-100.00m, "Store Purchase", baseDate.AddDays(3));

        // Act
        var confidence = _calculator.CalculateMatchConfidence(systemTransaction, bankTransaction);

        // Assert
        confidence.Should().BeLessThan(0.90m); // Should be reduced due to date difference
        confidence.Should().BeGreaterThan(0.60m); // But still reasonable due to amount and description match
    }

    [Fact]
    public void CalculateMatchConfidence_WithDifferentDescriptions_ReturnsLowerConfidence()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-100.00m, "Grocery Store", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-100.00m, "Gas Station", DateTime.UtcNow);

        // Act
        var confidence = _calculator.CalculateMatchConfidence(systemTransaction, bankTransaction);

        // Assert
        confidence.Should().BeLessThan(0.75m); // Lower due to description mismatch
    }

    [Fact]
    public void AnalyzeMatch_WithExactMatch_ReturnsCorrectAnalysis()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-100.50m, "Grocery Store", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-100.50m, "Grocery Store", DateTime.UtcNow);

        // Act
        var analysis = _calculator.AnalyzeMatch(systemTransaction, bankTransaction);

        // Assert
        analysis.AmountMatch.Should().BeTrue();
        analysis.AmountDifference.Should().Be(0);
        analysis.DateMatch.Should().BeTrue();
        analysis.DateDifferenceInDays.Should().Be(0);
        analysis.DescriptionSimilar.Should().BeTrue();
        analysis.DescriptionSimilarityScore.Should().BeGreaterThan(0.9m);
    }

    [Fact]
    public void AnalyzeMatch_WithPartialMatch_ReturnsCorrectAnalysis()
    {
        // Arrange
        var baseDate = DateTime.UtcNow.Date;
        var systemTransaction = CreateSystemTransaction(-100.00m, "Grocery Store ABC", baseDate);
        var bankTransaction = CreateBankTransaction(-100.25m, "Grocery Store XYZ", baseDate.AddDays(1));

        // Act
        var analysis = _calculator.AnalyzeMatch(systemTransaction, bankTransaction);

        // Assert
        analysis.AmountMatch.Should().BeFalse();
        analysis.AmountDifference.Should().Be(0.25m);
        analysis.DateMatch.Should().BeFalse();
        analysis.DateDifferenceInDays.Should().Be(1);
        analysis.DescriptionSimilar.Should().BeTrue(); // Should still be similar due to "Grocery Store"
        analysis.DescriptionSimilarityScore.Should().BeGreaterThan(0.6m);
    }

    [Fact]
    public void AnalyzeMatch_WithSimilarDescriptions_ReturnsCorrectSimilarityScore()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-50.00m, "Amazon Purchase", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-50.00m, "AMAZON.COM PURCHASE", DateTime.UtcNow);

        // Act
        var analysis = _calculator.AnalyzeMatch(systemTransaction, bankTransaction);

        // Assert
        analysis.DescriptionSimilar.Should().BeTrue();
        analysis.DescriptionSimilarityScore.Should().BeGreaterThan(0.7m);
    }

    [Fact]
    public void CalculateMatchConfidence_WithVeryLowSimilarity_AppliesPenalties()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-100.00m, "Grocery Store", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-200.00m, "Gas Station", DateTime.UtcNow.AddDays(10));

        // Act
        var confidence = _calculator.CalculateMatchConfidence(systemTransaction, bankTransaction);

        // Assert
        confidence.Should().BeLessThan(0.40m); // Should be very low due to multiple penalties
    }

    [Fact]
    public void AnalyzeMatch_WithSimilarDescriptions_DetectsSimilarity()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-50.00m, "McDonald's Restaurant", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-50.00m, "MCDONALD'S #123", DateTime.UtcNow);

        // Act
        var analysis = _calculator.AnalyzeMatch(systemTransaction, bankTransaction);

        // Assert
        analysis.DescriptionSimilar.Should().BeTrue();
        analysis.DescriptionSimilarityScore.Should().BeGreaterThan(0.1m);
    }

    [Fact]
    public void AnalyzeMatch_WithDifferentDescriptions_DetectsDifference()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-50.00m, "McDonald's Restaurant", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-50.00m, "Target Store", DateTime.UtcNow);

        // Act
        var analysis = _calculator.AnalyzeMatch(systemTransaction, bankTransaction);

        // Assert
        analysis.DescriptionSimilar.Should().BeFalse();
        analysis.DescriptionSimilarityScore.Should().BeLessThan(0.6m);
    }

    [Fact]
    public void CalculateMatchConfidence_WithNullOrEmptyDescriptions_HandlesGracefully()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-50.00m, "", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-50.00m, null, DateTime.UtcNow);

        // Act
        var confidence = _calculator.CalculateMatchConfidence(systemTransaction, bankTransaction);

        // Assert
        confidence.Should().BeGreaterThan(0); // Should not crash and return some confidence based on amount/date
        confidence.Should().BeLessThan(0.8m); // But should be lower due to missing descriptions
    }

    [Fact]
    public void AnalyzeMatch_WithPerfectAmountAndDateMatch_ButDifferentDescriptions_StillReturnsReasonableConfidence()
    {
        // Arrange
        var systemTransaction = CreateSystemTransaction(-75.99m, "Local Restaurant", DateTime.UtcNow);
        var bankTransaction = CreateBankTransaction(-75.99m, "Coffee Shop", DateTime.UtcNow);

        // Act
        var analysis = _calculator.AnalyzeMatch(systemTransaction, bankTransaction);
        var confidence = _calculator.CalculateMatchConfidence(systemTransaction, bankTransaction);

        // Assert
        analysis.AmountMatch.Should().BeTrue();
        analysis.DateMatch.Should().BeTrue();
        analysis.DescriptionSimilar.Should().BeFalse();
        confidence.Should().BeGreaterThan(0.60m); // Still reasonable due to amount and date match
        confidence.Should().BeLessThan(0.85m); // But penalized for description mismatch
    }

    private Transaction CreateSystemTransaction(decimal amount, string description, DateTime date)
    {
        return new Transaction
        {
            Id = 1,
            Amount = amount,
            Description = description,
            TransactionDate = date,
            AccountId = 1,
            Status = TransactionStatus.Cleared,
            CreatedBy = "test-user",
            UpdatedBy = "test-user"
        };
    }

    private BankTransactionDto CreateBankTransaction(decimal amount, string? description, DateTime date)
    {
        return new BankTransactionDto
        {
            BankTransactionId = "BANK_123",
            Amount = amount,
            Description = description ?? string.Empty,
            TransactionDate = date,
            BankCategory = "TEST_CATEGORY"
        };
    }
}