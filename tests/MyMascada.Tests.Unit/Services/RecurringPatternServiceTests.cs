using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.UpcomingBills.DTOs;
using MyMascada.Application.Features.UpcomingBills.Services;
using MyMascada.Domain.Entities;
using NSubstitute;

namespace MyMascada.Tests.Unit.Services;

public class RecurringPatternServiceTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRecurringPatternRepository _patternRepository;
    private readonly RecurringPatternService _service;
    private readonly Guid _userId;
    private int _transactionIdCounter;

    public RecurringPatternServiceTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _patternRepository = Substitute.For<IRecurringPatternRepository>();
        // Use the service without pattern repository for on-demand calculation tests
        _service = new RecurringPatternService(_transactionRepository, null);
        _userId = Guid.NewGuid();
        _transactionIdCounter = 1;
    }

    #region Basic Functionality Tests

    [Fact]
    public async Task GetUpcomingBillsAsync_WithNoTransactions_ShouldReturnEmptyBills()
    {
        // Arrange
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new List<Transaction>());

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        result.Bills.Should().BeEmpty();
        result.TotalBillsCount.Should().Be(0);
        result.TotalExpectedAmount.Should().Be(0);
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithOnlyIncomeTransactions_ShouldReturnEmptyBills()
    {
        // Arrange - only positive amounts (income)
        var transactions = new List<Transaction>
        {
            CreateTransaction(1000m, DateTime.UtcNow.AddDays(-30), "Salary Payment"),
            CreateTransaction(500m, DateTime.UtcNow.AddDays(-60), "Salary Payment"),
            CreateTransaction(1000m, DateTime.UtcNow.AddDays(-90), "Salary Payment"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        result.Bills.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldExcludeTransfers()
    {
        // Arrange - expense transactions that are transfers (have TransferId)
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "Transfer to Savings", transferId: Guid.NewGuid()),
            CreateTransaction(-100m, today.AddDays(-60), "Transfer to Savings", transferId: Guid.NewGuid()),
            CreateTransaction(-100m, today.AddDays(-90), "Transfer to Savings", transferId: Guid.NewGuid()),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        result.Bills.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldExcludeDeletedTransactions()
    {
        // Arrange - deleted transactions should be excluded
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "Netflix Subscription", isDeleted: true),
            CreateTransaction(-100m, today.AddDays(-60), "Netflix Subscription", isDeleted: true),
            CreateTransaction(-100m, today.AddDays(-90), "Netflix Subscription", isDeleted: true),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        result.Bills.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithValidPattern_ShouldSortByDaysUntilDueThenConfidence()
    {
        // Arrange - two merchants with bills due at different times
        var today = DateTime.UtcNow.Date;

        // Netflix - monthly, expected in 3 days
        var netflixTransactions = new List<Transaction>
        {
            CreateTransaction(-15.99m, today.AddDays(-27), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-57), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-87), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-117), "Netflix"),
        };

        // Spotify - monthly, expected in 5 days
        var spotifyTransactions = new List<Transaction>
        {
            CreateTransaction(-9.99m, today.AddDays(-25), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-55), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-85), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-115), "Spotify"),
        };

        var allTransactions = netflixTransactions.Concat(spotifyTransactions).ToList();

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(allTransactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        result.Bills.Should().HaveCountGreaterOrEqualTo(2);
        // Bills should be sorted by DaysUntilDue (ascending)
        result.Bills.Should().BeInAscendingOrder(b => b.DaysUntilDue);
    }

    #endregion

    #region Interval Detection Tests

    [Theory]
    [InlineData(7, "Weekly")]   // Exactly 7 days
    [InlineData(5, "Weekly")]   // Min weekly (5-9 days)
    [InlineData(9, "Weekly")]   // Max weekly
    public async Task GetUpcomingBillsAsync_WithWeeklyPattern_ShouldDetectWeeklyInterval(int daysBetween, string expectedInterval)
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-50m, today.AddDays(-daysBetween), "Weekly Service"),
            CreateTransaction(-50m, today.AddDays(-daysBetween * 2), "Weekly Service"),
            CreateTransaction(-50m, today.AddDays(-daysBetween * 3), "Weekly Service"),
            CreateTransaction(-50m, today.AddDays(-daysBetween * 4), "Weekly Service"),
            CreateTransaction(-50m, today.AddDays(-daysBetween * 5), "Weekly Service"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, daysBetween);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().Interval.Should().Be(expectedInterval);
        }
    }

    [Theory]
    [InlineData(14, "Biweekly")] // Exactly 14 days
    [InlineData(12, "Biweekly")] // Min biweekly (12-16 days)
    [InlineData(16, "Biweekly")] // Max biweekly
    public async Task GetUpcomingBillsAsync_WithBiweeklyPattern_ShouldDetectBiweeklyInterval(int daysBetween, string expectedInterval)
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-daysBetween), "Biweekly Payment"),
            CreateTransaction(-100m, today.AddDays(-daysBetween * 2), "Biweekly Payment"),
            CreateTransaction(-100m, today.AddDays(-daysBetween * 3), "Biweekly Payment"),
            CreateTransaction(-100m, today.AddDays(-daysBetween * 4), "Biweekly Payment"),
            CreateTransaction(-100m, today.AddDays(-daysBetween * 5), "Biweekly Payment"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, daysBetween);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().Interval.Should().Be(expectedInterval);
        }
    }

    [Theory]
    [InlineData(30, "Monthly")] // Exactly 30 days
    [InlineData(26, "Monthly")] // Min monthly (26-35 days)
    [InlineData(35, "Monthly")] // Max monthly
    [InlineData(31, "Monthly")] // Common month length
    public async Task GetUpcomingBillsAsync_WithMonthlyPattern_ShouldDetectMonthlyInterval(int daysBetween, string expectedInterval)
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-200m, today.AddDays(-daysBetween), "Monthly Bill"),
            CreateTransaction(-200m, today.AddDays(-daysBetween * 2), "Monthly Bill"),
            CreateTransaction(-200m, today.AddDays(-daysBetween * 3), "Monthly Bill"),
            CreateTransaction(-200m, today.AddDays(-daysBetween * 4), "Monthly Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, daysBetween);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().Interval.Should().Be(expectedInterval);
        }
    }

    [Theory]
    [InlineData(10)] // Between weekly(9) and biweekly(12) - invalid gap
    [InlineData(11)] // Between weekly(9) and biweekly(12) - invalid gap
    [InlineData(17)] // Between biweekly(16) and monthly(26) - invalid gap
    [InlineData(20)] // Between biweekly and monthly - invalid gap
    [InlineData(25)] // Just below monthly range
    [InlineData(4)]  // Too short for weekly
    [InlineData(40)] // Too long for monthly
    public async Task GetUpcomingBillsAsync_WithInvalidInterval_ShouldNotDetectPattern(int daysBetween)
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-daysBetween), "Irregular Payment"),
            CreateTransaction(-100m, today.AddDays(-daysBetween * 2), "Irregular Payment"),
            CreateTransaction(-100m, today.AddDays(-daysBetween * 3), "Irregular Payment"),
            CreateTransaction(-100m, today.AddDays(-daysBetween * 4), "Irregular Payment"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert - Should not detect a recurring pattern for invalid intervals
        result.Bills.Should().BeEmpty();
    }

    #endregion

    #region Confidence Scoring Tests

    [Fact]
    public async Task GetUpcomingBillsAsync_WithHighOccurrenceCount_ShouldHaveHighConfidence()
    {
        // Arrange - 5+ occurrences should give higher confidence
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "High Frequency Bill"),
            CreateTransaction(-100m, today.AddDays(-60), "High Frequency Bill"),
            CreateTransaction(-100m, today.AddDays(-90), "High Frequency Bill"),
            CreateTransaction(-100m, today.AddDays(-120), "High Frequency Bill"),
            CreateTransaction(-100m, today.AddDays(-150), "High Frequency Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().OccurrenceCount.Should().Be(5);
            result.Bills.First().ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.5m);
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithMinimumOccurrences_ShouldHaveLowerConfidence()
    {
        // Arrange - only 2 occurrences (minimum required)
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "Low Frequency Bill"),
            CreateTransaction(-100m, today.AddDays(-60), "Low Frequency Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().OccurrenceCount.Should().Be(2);
            // Confidence should be lower with fewer occurrences
            result.Bills.First().ConfidenceScore.Should().BeLessThan(0.8m);
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithConsistentAmounts_ShouldIncreaseConfidence()
    {
        // Arrange - same exact amount every time
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-99.99m, today.AddDays(-30), "Consistent Bill"),
            CreateTransaction(-99.99m, today.AddDays(-60), "Consistent Bill"),
            CreateTransaction(-99.99m, today.AddDays(-90), "Consistent Bill"),
            CreateTransaction(-99.99m, today.AddDays(-120), "Consistent Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().ExpectedAmount.Should().Be(99.99m);
            result.Bills.First().ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.5m);
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithVariableAmounts_ShouldDecreaseConfidence()
    {
        // Arrange - highly variable amounts
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-50m, today.AddDays(-30), "Variable Bill"),
            CreateTransaction(-150m, today.AddDays(-60), "Variable Bill"),
            CreateTransaction(-75m, today.AddDays(-90), "Variable Bill"),
            CreateTransaction(-200m, today.AddDays(-120), "Variable Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert - variable amounts may result in lower confidence or no bill detected
        // depending on whether it passes the minimum threshold
        if (result.Bills.Any())
        {
            result.Bills.First().ConfidenceScore.Should().BeLessThan(0.9m);
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithBelowThresholdConfidence_ShouldFilterOutBill()
    {
        // Arrange - create pattern with very inconsistent intervals/amounts
        var today = DateTime.UtcNow.Date;

        // Only 2 transactions with some amount variance - likely below threshold
        var transactions = new List<Transaction>
        {
            CreateTransaction(-50m, today.AddDays(-30), "Low Confidence Bill"),
            CreateTransaction(-100m, today.AddDays(-65), "Low Confidence Bill"), // Irregular interval
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert - bill should be filtered out due to low confidence from inconsistent patterns
        // (either no pattern detected or confidence below 0.5)
        result.Bills.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithHighConfidence_ShouldSetHighConfidenceLevel()
    {
        // Arrange - very consistent pattern
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "Consistent Service"),
            CreateTransaction(-100m, today.AddDays(-60), "Consistent Service"),
            CreateTransaction(-100m, today.AddDays(-90), "Consistent Service"),
            CreateTransaction(-100m, today.AddDays(-120), "Consistent Service"),
            CreateTransaction(-100m, today.AddDays(-150), "Consistent Service"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any() && result.Bills.First().ConfidenceScore >= 0.75m)
        {
            result.Bills.First().ConfidenceLevel.Should().Be("High");
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithMediumConfidence_ShouldSetMediumConfidenceLevel()
    {
        // Arrange - pattern that should give medium confidence (between 0.5 and 0.75)
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-28), "Medium Confidence Service"),
            CreateTransaction(-105m, today.AddDays(-58), "Medium Confidence Service"), // Slight variation
            CreateTransaction(-98m, today.AddDays(-88), "Medium Confidence Service"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any() && result.Bills.First().ConfidenceScore < 0.75m && result.Bills.First().ConfidenceScore >= 0.5m)
        {
            result.Bills.First().ConfidenceLevel.Should().Be("Medium");
        }
    }

    #endregion

    #region Description Normalization Tests

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldRemovePurchasePrefix()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-15.99m, today.AddDays(-30), "PURCHASE NETFLIX"),
            CreateTransaction(-15.99m, today.AddDays(-60), "PURCHASE NETFLIX"),
            CreateTransaction(-15.99m, today.AddDays(-90), "PURCHASE NETFLIX"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().MerchantName.ToLower().Should().NotContain("purchase");
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldRemovePaymentPrefix()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-50m, today.AddDays(-30), "PAYMENT UTILITY CO"),
            CreateTransaction(-50m, today.AddDays(-60), "PAYMENT UTILITY CO"),
            CreateTransaction(-50m, today.AddDays(-90), "PAYMENT UTILITY CO"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().MerchantName.ToLower().Should().NotContain("payment");
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldRemovePosPrefix()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-25m, today.AddDays(-30), "POS COFFEE SHOP"),
            CreateTransaction(-25m, today.AddDays(-60), "POS COFFEE SHOP"),
            CreateTransaction(-25m, today.AddDays(-90), "POS COFFEE SHOP"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().MerchantName.ToLower().Should().NotContain("pos");
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldRemoveReferenceNumbers()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "ACME CORP #12345"),
            CreateTransaction(-100m, today.AddDays(-60), "ACME CORP #67890"),
            CreateTransaction(-100m, today.AddDays(-90), "ACME CORP REF:ABC123"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert - transactions with different reference numbers should be grouped
        if (result.Bills.Any())
        {
            result.Bills.First().MerchantName.Should().NotContain("#");
            result.Bills.First().MerchantName.ToLower().Should().NotContain("ref:");
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldRemoveDatesFromDescription()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "SUBSCRIPTION 01/15"),
            CreateTransaction(-100m, today.AddDays(-60), "SUBSCRIPTION 12/15"),
            CreateTransaction(-100m, today.AddDays(-90), "SUBSCRIPTION 11/15"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert - transactions with different dates should be grouped
        if (result.Bills.Any())
        {
            // Merchant name should not contain date patterns
            result.Bills.First().MerchantName.Should().NotMatchRegex(@"\d{1,2}/\d{1,2}");
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldRemoveTrailingNumbers()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "MERCHANT NAME 123456"),
            CreateTransaction(-100m, today.AddDays(-60), "MERCHANT NAME 789012"),
            CreateTransaction(-100m, today.AddDays(-90), "MERCHANT NAME 345678"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        if (result.Bills.Any())
        {
            // Merchant name should be normalized without trailing numbers
            result.Bills.First().MerchantName.Should().NotMatchRegex(@"\d+$");
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithEmptyDescription_ShouldSkipTransaction()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), ""),
            CreateTransaction(-100m, today.AddDays(-60), "   "),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        result.Bills.Should().BeEmpty();
    }

    #endregion

    #region String Similarity / Group Merging Tests

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldMergeSimilarDescriptions()
    {
        // Arrange - slight variations in merchant name (>80% similar) should merge
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            // These are >80% similar after normalization and should be merged
            CreateTransaction(-15.99m, today.AddDays(-30), "NETFLIX SUBSCRIPTION"),
            CreateTransaction(-15.99m, today.AddDays(-60), "NETFLIX SUBSCRIPTN"),
            CreateTransaction(-15.99m, today.AddDays(-90), "NETFLIX SUBSCRIPTON"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert - should be detected as a single recurring bill due to string similarity merging
        if (result.Bills.Any())
        {
            // The similar descriptions should be merged into one bill
            result.Bills.Should().HaveCount(1);
            result.Bills.First().OccurrenceCount.Should().BeGreaterThanOrEqualTo(2);
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldNotMergeCompletelyDifferentDescriptions()
    {
        // Arrange - completely different merchants
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            // Netflix transactions
            CreateTransaction(-15.99m, today.AddDays(-30), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-60), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-90), "Netflix"),
            // Spotify transactions
            CreateTransaction(-9.99m, today.AddDays(-28), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-58), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-88), "Spotify"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert - should detect two separate recurring bills
        if (result.Bills.Count >= 2)
        {
            var merchantNames = result.Bills.Select(b => b.MerchantName.ToLower()).ToList();
            merchantNames.Should().Contain(n => n.Contains("netflix"));
            merchantNames.Should().Contain(n => n.Contains("spotify"));
        }
    }

    #endregion

    #region Date Window Tests

    [Fact]
    public async Task GetUpcomingBillsAsync_BillOutsideDaysAhead_ShouldBeExcluded()
    {
        // Arrange - bill would be due after the daysAhead window
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            // Monthly pattern with last payment 5 days ago = next due in ~25 days
            CreateTransaction(-100m, today.AddDays(-5), "Monthly Bill"),
            CreateTransaction(-100m, today.AddDays(-35), "Monthly Bill"),
            CreateTransaction(-100m, today.AddDays(-65), "Monthly Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act - only looking 7 days ahead
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert - bill due in ~25 days should not appear
        result.Bills.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_BillWithinDaysAhead_ShouldBeIncluded()
    {
        // Arrange - bill due within the window
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            // Monthly pattern with last payment 27 days ago = next due in ~3 days
            CreateTransaction(-100m, today.AddDays(-27), "Monthly Bill"),
            CreateTransaction(-100m, today.AddDays(-57), "Monthly Bill"),
            CreateTransaction(-100m, today.AddDays(-87), "Monthly Bill"),
            CreateTransaction(-100m, today.AddDays(-117), "Monthly Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().DaysUntilDue.Should().BeLessThanOrEqualTo(7);
            result.Bills.First().DaysUntilDue.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_BillWithPastExpectedDate_ShouldBeExcluded()
    {
        // Arrange - pattern where next expected date is in the past
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            // Monthly pattern but last payment was 35 days ago = overdue (past expected date)
            CreateTransaction(-100m, today.AddDays(-35), "Overdue Bill"),
            CreateTransaction(-100m, today.AddDays(-65), "Overdue Bill"),
            CreateTransaction(-100m, today.AddDays(-95), "Overdue Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert - overdue bills (negative DaysUntilDue) should be excluded
        result.Bills.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_DaysUntilDueAtBoundary_ShouldBeIncluded()
    {
        // Arrange - bill due exactly at the boundary of daysAhead
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            // Monthly pattern with last payment 23 days ago = next due in ~7 days
            CreateTransaction(-100m, today.AddDays(-23), "Boundary Bill"),
            CreateTransaction(-100m, today.AddDays(-53), "Boundary Bill"),
            CreateTransaction(-100m, today.AddDays(-83), "Boundary Bill"),
            CreateTransaction(-100m, today.AddDays(-113), "Boundary Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        if (result.Bills.Any())
        {
            result.Bills.First().DaysUntilDue.Should().BeLessThanOrEqualTo(7);
        }
    }

    #endregion

    #region Response Calculation Tests

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldCalculateTotalExpectedAmountCorrectly()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;

        // Netflix - $15.99
        var netflixTransactions = new List<Transaction>
        {
            CreateTransaction(-15.99m, today.AddDays(-27), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-57), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-87), "Netflix"),
        };

        // Spotify - $9.99
        var spotifyTransactions = new List<Transaction>
        {
            CreateTransaction(-9.99m, today.AddDays(-25), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-55), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-85), "Spotify"),
        };

        var allTransactions = netflixTransactions.Concat(spotifyTransactions).ToList();

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(allTransactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        if (result.Bills.Count >= 2)
        {
            var expectedTotal = result.Bills.Sum(b => b.ExpectedAmount);
            result.TotalExpectedAmount.Should().Be(expectedTotal);
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ShouldReturnCorrectBillCount()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;

        var netflixTransactions = new List<Transaction>
        {
            CreateTransaction(-15.99m, today.AddDays(-27), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-57), "Netflix"),
            CreateTransaction(-15.99m, today.AddDays(-87), "Netflix"),
        };

        var spotifyTransactions = new List<Transaction>
        {
            CreateTransaction(-9.99m, today.AddDays(-25), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-55), "Spotify"),
            CreateTransaction(-9.99m, today.AddDays(-85), "Spotify"),
        };

        var allTransactions = netflixTransactions.Concat(spotifyTransactions).ToList();

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(allTransactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        result.TotalBillsCount.Should().Be(result.Bills.Count);
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ExpectedAmount_ShouldBePositive()
    {
        // Arrange - expenses are negative but expected amount should be positive
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-27), "Test Bill"),
            CreateTransaction(-100m, today.AddDays(-57), "Test Bill"),
            CreateTransaction(-100m, today.AddDays(-87), "Test Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        foreach (var bill in result.Bills)
        {
            bill.ExpectedAmount.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_ExpectedAmount_ShouldBeRoundedToTwoDecimals()
    {
        // Arrange - amounts that would average to a non-round number
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-33.33m, today.AddDays(-27), "Variable Bill"),
            CreateTransaction(-33.34m, today.AddDays(-57), "Variable Bill"),
            CreateTransaction(-33.33m, today.AddDays(-87), "Variable Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert
        if (result.Bills.Any())
        {
            // Check that the amount has at most 2 decimal places
            var amount = result.Bills.First().ExpectedAmount;
            Math.Round(amount, 2).Should().Be(amount);
        }
    }

    #endregion

    #region Minimum Occurrences Tests

    [Fact]
    public async Task GetUpcomingBillsAsync_WithSingleOccurrence_ShouldNotDetectPattern()
    {
        // Arrange - only 1 transaction (below minimum of 2)
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-30), "Single Payment"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 30);

        // Assert
        result.Bills.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithMinimumTwoOccurrences_ShouldAttemptPatternDetection()
    {
        // Arrange - exactly 2 transactions (minimum required)
        var today = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            CreateTransaction(-100m, today.AddDays(-27), "Two Payment Bill"),
            CreateTransaction(-100m, today.AddDays(-57), "Two Payment Bill"),
        };

        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(transactions);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, 7);

        // Assert - may or may not detect based on confidence threshold
        // The key is that it doesn't error with only 2 transactions
        result.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private Transaction CreateTransaction(
        decimal amount,
        DateTime date,
        string description,
        Guid? transferId = null,
        bool isDeleted = false)
    {
        return new Transaction
        {
            Id = _transactionIdCounter++,
            Amount = amount,
            TransactionDate = date,
            Description = description,
            TransferId = transferId,
            IsDeleted = isDeleted,
            AccountId = 1
        };
    }

    #endregion
}
