using MyMascada.Domain.Enums;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Domain;

public class RecurringPatternTests
{
    #region Grace Window Calculation Tests

    [Fact]
    public void GetGraceWindowEnd_WithMonthlyInterval_ShouldReturn30PercentAfterExpectedDate()
    {
        // Arrange
        var pattern = CreatePattern(intervalDays: 30, nextExpectedDate: new DateTime(2024, 2, 1));

        // Act
        var graceWindowEnd = pattern.GetGraceWindowEnd();

        // Assert
        // Grace window = NextExpectedDate + (IntervalDays * 0.3) = Feb 1 + 9 days = Feb 10
        graceWindowEnd.Should().Be(new DateTime(2024, 2, 10));
    }

    [Fact]
    public void GetGraceWindowEnd_WithWeeklyInterval_ShouldReturnCorrectGraceWindow()
    {
        // Arrange
        var pattern = CreatePattern(intervalDays: 7, nextExpectedDate: new DateTime(2024, 2, 1));

        // Act
        var graceWindowEnd = pattern.GetGraceWindowEnd();

        // Assert
        // Grace window = Feb 1 + (7 * 0.3) = Feb 1 + 2.1 days = Feb 3
        var expectedEnd = new DateTime(2024, 2, 1).AddDays(7 * 0.3);
        graceWindowEnd.Should().Be(expectedEnd);
    }

    [Theory]
    [InlineData(30, 9)]   // Monthly: 30 * 0.3 = 9 days
    [InlineData(14, 4.2)] // Biweekly: 14 * 0.3 = 4.2 days
    [InlineData(7, 2.1)]  // Weekly: 7 * 0.3 = 2.1 days
    public void GetGraceWindowEnd_ShouldCalculateCorrectGracePeriod(int intervalDays, double expectedGraceDays)
    {
        // Arrange
        var pattern = CreatePattern(intervalDays: intervalDays, nextExpectedDate: new DateTime(2024, 2, 1));

        // Act
        var graceWindowEnd = pattern.GetGraceWindowEnd();

        // Assert
        var expectedEnd = new DateTime(2024, 2, 1).AddDays(expectedGraceDays);
        graceWindowEnd.Should().Be(expectedEnd);
    }

    [Fact]
    public void IsWithinGraceWindow_WhenCurrentDateBeforeEnd_ShouldReturnTrue()
    {
        // Arrange
        var pattern = CreatePattern(intervalDays: 30, nextExpectedDate: new DateTime(2024, 2, 1));
        var currentDate = new DateTime(2024, 2, 5); // 5 days after, grace window is 9 days

        // Act
        var result = pattern.IsWithinGraceWindow(currentDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinGraceWindow_WhenCurrentDateAfterEnd_ShouldReturnFalse()
    {
        // Arrange
        var pattern = CreatePattern(intervalDays: 30, nextExpectedDate: new DateTime(2024, 2, 1));
        var currentDate = new DateTime(2024, 2, 15); // 15 days after, grace window is 9 days

        // Act
        var result = pattern.IsWithinGraceWindow(currentDate);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public void ShouldMarkAtRisk_WhenActiveAndPastGraceWindow_ShouldReturnTrue()
    {
        // Arrange
        var pattern = CreatePattern(
            status: RecurringPatternStatus.Active,
            intervalDays: 30,
            nextExpectedDate: DateTime.UtcNow.AddDays(-15),
            consecutiveMisses: 0);

        // Act
        var result = pattern.ShouldMarkAtRisk(DateTime.UtcNow);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldMarkAtRisk_WhenActiveButWithinGraceWindow_ShouldReturnFalse()
    {
        // Arrange
        var pattern = CreatePattern(
            status: RecurringPatternStatus.Active,
            intervalDays: 30,
            nextExpectedDate: DateTime.UtcNow.AddDays(-3),
            consecutiveMisses: 0);

        // Act
        var result = pattern.ShouldMarkAtRisk(DateTime.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldMarkAtRisk_WhenAlreadyAtRisk_ShouldReturnFalse()
    {
        // Arrange
        var pattern = CreatePattern(
            status: RecurringPatternStatus.AtRisk,
            intervalDays: 30,
            nextExpectedDate: DateTime.UtcNow.AddDays(-15),
            consecutiveMisses: 1);

        // Act
        var result = pattern.ShouldMarkAtRisk(DateTime.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldMarkCancelled_WhenAtRiskAndSecondMiss_ShouldReturnTrue()
    {
        // Arrange
        var pattern = CreatePattern(
            status: RecurringPatternStatus.AtRisk,
            intervalDays: 30,
            nextExpectedDate: DateTime.UtcNow.AddDays(-15),
            consecutiveMisses: 1);

        // Act
        var result = pattern.ShouldMarkCancelled(DateTime.UtcNow);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldMarkCancelled_WhenActiveWithOneMiss_ShouldReturnFalse()
    {
        // Arrange
        var pattern = CreatePattern(
            status: RecurringPatternStatus.Active,
            intervalDays: 30,
            nextExpectedDate: DateTime.UtcNow.AddDays(-15),
            consecutiveMisses: 0);

        // Act
        var result = pattern.ShouldMarkCancelled(DateTime.UtcNow);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RecordMiss_WithFirstMiss_ShouldSetStatusToAtRisk()
    {
        // Arrange
        var pattern = CreatePattern(
            status: RecurringPatternStatus.Active,
            consecutiveMisses: 0);

        // Act
        pattern.RecordMiss(DateTime.UtcNow);

        // Assert
        pattern.ConsecutiveMisses.Should().Be(1);
        pattern.Status.Should().Be(RecurringPatternStatus.AtRisk);
    }

    [Fact]
    public void RecordMiss_WithSecondMiss_ShouldSetStatusToCancelled()
    {
        // Arrange
        var pattern = CreatePattern(
            status: RecurringPatternStatus.AtRisk,
            consecutiveMisses: 1);

        // Act
        pattern.RecordMiss(DateTime.UtcNow);

        // Assert
        pattern.ConsecutiveMisses.Should().Be(2);
        pattern.Status.Should().Be(RecurringPatternStatus.Cancelled);
    }

    [Fact]
    public void RecordMatch_ShouldResetConsecutiveMisses()
    {
        // Arrange
        var pattern = CreatePattern(
            status: RecurringPatternStatus.AtRisk,
            consecutiveMisses: 1,
            averageAmount: 100m,
            occurrenceCount: 5);

        // Act
        pattern.RecordMatch(DateTime.UtcNow, -100m);

        // Assert
        pattern.ConsecutiveMisses.Should().Be(0);
        pattern.Status.Should().Be(RecurringPatternStatus.Active);
    }

    [Fact]
    public void RecordMatch_ShouldUpdateNextExpectedDate()
    {
        // Arrange
        var matchDate = DateTime.UtcNow;
        var pattern = CreatePattern(intervalDays: 30);

        // Act
        pattern.RecordMatch(matchDate, -100m);

        // Assert
        pattern.NextExpectedDate.Should().Be(matchDate.AddDays(30));
    }

    [Fact]
    public void RecordMatch_ShouldIncrementOccurrenceCount()
    {
        // Arrange
        var pattern = CreatePattern(occurrenceCount: 5, averageAmount: 100m);

        // Act
        pattern.RecordMatch(DateTime.UtcNow, -100m);

        // Assert
        pattern.OccurrenceCount.Should().Be(6);
    }

    [Fact]
    public void RecordMatch_ShouldUpdateAverageAmount()
    {
        // Arrange - 5 occurrences at $100 average
        var pattern = CreatePattern(occurrenceCount: 5, averageAmount: 100m);

        // Act - 6th occurrence at $160 (absolute value)
        pattern.RecordMatch(DateTime.UtcNow, -160m);

        // Assert - New average: ((100 * 5) + 160) / 6 = 110
        pattern.AverageAmount.Should().Be(110m);
    }

    [Fact]
    public void Pause_ShouldSetStatusToPaused()
    {
        // Arrange
        var pattern = CreatePattern(status: RecurringPatternStatus.Active);

        // Act
        pattern.Pause();

        // Assert
        pattern.Status.Should().Be(RecurringPatternStatus.Paused);
    }

    [Fact]
    public void Resume_ShouldSetStatusToActive()
    {
        // Arrange
        var pattern = CreatePattern(status: RecurringPatternStatus.Paused);

        // Act
        pattern.Resume();

        // Assert
        pattern.Status.Should().Be(RecurringPatternStatus.Active);
    }

    [Fact]
    public void Cancel_ShouldSetStatusToCancelled()
    {
        // Arrange
        var pattern = CreatePattern(status: RecurringPatternStatus.Active);

        // Act
        pattern.Cancel();

        // Assert
        pattern.Status.Should().Be(RecurringPatternStatus.Cancelled);
    }

    #endregion

    #region Interval and Confidence Display Tests

    [Theory]
    [InlineData(7, "Weekly")]
    [InlineData(9, "Weekly")]
    [InlineData(14, "Biweekly")]
    [InlineData(16, "Biweekly")]
    [InlineData(30, "Monthly")]
    [InlineData(35, "Monthly")]
    [InlineData(45, "Every 45 days")]
    public void GetIntervalName_ShouldReturnCorrectName(int intervalDays, string expectedName)
    {
        // Arrange
        var pattern = CreatePattern(intervalDays: intervalDays);

        // Act
        var result = pattern.GetIntervalName();

        // Assert
        result.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(0.80, "High")]
    [InlineData(0.75, "High")]
    [InlineData(0.74, "Medium")]
    [InlineData(0.50, "Medium")]
    [InlineData(0.49, "Low")]
    public void GetConfidenceLevel_ShouldReturnCorrectLevel(decimal confidence, string expectedLevel)
    {
        // Arrange
        var pattern = CreatePattern(confidence: confidence);

        // Act
        var result = pattern.GetConfidenceLevel();

        // Assert
        result.Should().Be(expectedLevel);
    }

    #endregion

    #region Cost Calculation Tests

    [Theory]
    [InlineData(100, 30, 1216.67)]  // Monthly: 100 * (365/30) = 1216.67
    [InlineData(50, 14, 1303.57)]   // Biweekly: 50 * (365/14) = 1303.57
    [InlineData(25, 7, 1303.57)]    // Weekly: 25 * (365/7) = 1303.57
    public void GetAnnualCost_ShouldCalculateCorrectly(decimal averageAmount, int intervalDays, decimal expectedAnnualCost)
    {
        // Arrange
        var pattern = CreatePattern(averageAmount: averageAmount, intervalDays: intervalDays);

        // Act
        var result = pattern.GetAnnualCost();

        // Assert
        result.Should().BeApproximately(expectedAnnualCost, 0.01m);
    }

    [Theory]
    [InlineData(100, 30, 101.47)]   // Monthly: 100 * (30.44/30) = 101.47
    [InlineData(50, 14, 108.71)]    // Biweekly: 50 * (30.44/14) = 108.71
    [InlineData(25, 7, 108.71)]     // Weekly: 25 * (30.44/7) = 108.71
    public void GetMonthlyCost_ShouldCalculateCorrectly(decimal averageAmount, int intervalDays, decimal expectedMonthlyCost)
    {
        // Arrange
        var pattern = CreatePattern(averageAmount: averageAmount, intervalDays: intervalDays);

        // Act
        var result = pattern.GetMonthlyCost();

        // Assert
        result.Should().BeApproximately(expectedMonthlyCost, 0.01m);
    }

    [Fact]
    public void GetAnnualCost_WithZeroInterval_ShouldReturnZero()
    {
        // Arrange
        var pattern = CreatePattern(intervalDays: 0);

        // Act
        var result = pattern.GetAnnualCost();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetDaysUntilDue_ShouldReturnCorrectDays()
    {
        // Arrange
        var pattern = CreatePattern(nextExpectedDate: DateTime.UtcNow.AddDays(5).Date);

        // Act
        var result = pattern.GetDaysUntilDue(DateTime.UtcNow.Date);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void GetDaysUntilDue_WhenOverdue_ShouldReturnNegative()
    {
        // Arrange
        var pattern = CreatePattern(nextExpectedDate: DateTime.UtcNow.AddDays(-3).Date);

        // Act
        var result = pattern.GetDaysUntilDue(DateTime.UtcNow.Date);

        // Assert
        result.Should().Be(-3);
    }

    #endregion

    #region Transaction Matching Tests

    [Fact]
    public void MatchesTransaction_WithSimilarDescriptionAndAmount_ShouldReturnTrue()
    {
        // Arrange
        var pattern = CreatePattern(
            normalizedMerchantKey: "netflix subscription",
            averageAmount: 15.99m);

        // Act
        var result = pattern.MatchesTransaction("NETFLIX SUBSCRIPTION", -15.99m);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesTransaction_WithDifferentDescription_ShouldReturnFalse()
    {
        // Arrange
        var pattern = CreatePattern(
            normalizedMerchantKey: "netflix subscription",
            averageAmount: 15.99m);

        // Act
        var result = pattern.MatchesTransaction("Spotify Premium", -9.99m);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesTransaction_WithAmountOutsideRange_ShouldReturnFalse()
    {
        // Arrange
        var pattern = CreatePattern(
            normalizedMerchantKey: "netflix subscription",
            averageAmount: 15.99m);

        // Act - Amount is more than 20% higher
        var result = pattern.MatchesTransaction("NETFLIX SUBSCRIPTION", -25.00m);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MatchesTransaction_WithAmountWithin20Percent_ShouldReturnTrue()
    {
        // Arrange
        var pattern = CreatePattern(
            normalizedMerchantKey: "netflix subscription",
            averageAmount: 15.99m);

        // Act - Amount is within 20% (15.99 * 1.2 = 19.19)
        var result = pattern.MatchesTransaction("NETFLIX SUBSCRIPTION", -18.00m);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void MatchesTransaction_WithEmptyDescription_ShouldReturnFalse()
    {
        // Arrange
        var pattern = CreatePattern(
            normalizedMerchantKey: "netflix subscription",
            averageAmount: 15.99m);

        // Act
        var result = pattern.MatchesTransaction("", -15.99m);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Description Normalization Tests

    [Theory]
    [InlineData("PURCHASE NETFLIX", "netflix")]
    [InlineData("PAYMENT UTILITY CO", "utility co")]
    [InlineData("POS COFFEE SHOP", "coffee shop")]
    [InlineData("DEBIT AMAZON PRIME", "amazon prime")]
    public void NormalizeDescription_ShouldRemoveCommonPrefixes(string input, string expectedOutput)
    {
        // Act
        var result = RecurringPattern.NormalizeDescription(input);

        // Assert
        result.Should().Be(expectedOutput);
    }

    [Theory]
    [InlineData("MERCHANT #12345", "merchant")]
    [InlineData("ACME CORP REF:ABC123", "acme corp")]
    [InlineData("STORE ID:987654", "store")]
    public void NormalizeDescription_ShouldRemoveReferenceNumbers(string input, string expectedOutput)
    {
        // Act
        var result = RecurringPattern.NormalizeDescription(input);

        // Assert
        result.Should().Be(expectedOutput);
    }

    [Theory]
    [InlineData("SUBSCRIPTION 01/15", "subscription")]
    [InlineData("NETFLIX 12-25-2024", "netflix")]
    [InlineData("SPOTIFY 01/15/2024 SUBSCRIPTION", "spotify subscription")]
    public void NormalizeDescription_ShouldRemoveDates(string input, string expectedOutput)
    {
        // Act
        var result = RecurringPattern.NormalizeDescription(input);

        // Assert
        result.Should().Be(expectedOutput);
    }

    [Fact]
    public void NormalizeDescription_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var result = RecurringPattern.NormalizeDescription("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeDescription_WithNull_ShouldReturnEmpty()
    {
        // Act
        var result = RecurringPattern.NormalizeDescription(null);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static RecurringPattern CreatePattern(
        RecurringPatternStatus status = RecurringPatternStatus.Active,
        int intervalDays = 30,
        DateTime? nextExpectedDate = null,
        int consecutiveMisses = 0,
        decimal averageAmount = 100m,
        decimal confidence = 0.8m,
        int occurrenceCount = 3,
        string normalizedMerchantKey = "test merchant")
    {
        return new RecurringPattern
        {
            Id = 1,
            UserId = Guid.NewGuid(),
            MerchantName = "Test Merchant",
            NormalizedMerchantKey = normalizedMerchantKey,
            IntervalDays = intervalDays,
            AverageAmount = averageAmount,
            Confidence = confidence,
            Status = status,
            NextExpectedDate = nextExpectedDate ?? DateTime.UtcNow.AddDays(3),
            LastObservedAt = DateTime.UtcNow.AddDays(-27),
            ConsecutiveMisses = consecutiveMisses,
            OccurrenceCount = occurrenceCount
        };
    }

    #endregion
}
