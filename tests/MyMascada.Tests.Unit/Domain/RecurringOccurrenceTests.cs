using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Domain;

public class RecurringOccurrenceTests
{
    #region Factory Method Tests

    [Fact]
    public void CreateMissed_ShouldCreateOccurrenceWithCorrectProperties()
    {
        // Arrange
        var patternId = 1;
        var expectedDate = DateTime.UtcNow;
        var expectedAmount = 100m;

        // Act
        var occurrence = RecurringOccurrence.CreateMissed(patternId, expectedDate, expectedAmount);

        // Assert
        occurrence.PatternId.Should().Be(patternId);
        occurrence.ExpectedDate.Should().Be(expectedDate);
        occurrence.ExpectedAmount.Should().Be(expectedAmount);
        occurrence.Outcome.Should().Be(OccurrenceOutcome.Missed);
        occurrence.TransactionId.Should().BeNull();
        occurrence.ActualDate.Should().BeNull();
        occurrence.ActualAmount.Should().BeNull();
    }

    [Fact]
    public void CreatePosted_WithOnTimePayment_ShouldSetPostedOutcome()
    {
        // Arrange
        var patternId = 1;
        var expectedDate = new DateTime(2024, 2, 1);
        var actualDate = new DateTime(2024, 2, 1); // Same day
        var expectedAmount = 100m;
        var actualAmount = 100m;
        var transactionId = 123;

        // Act
        var occurrence = RecurringOccurrence.CreatePosted(
            patternId, expectedDate, expectedAmount,
            transactionId, actualDate, actualAmount);

        // Assert
        occurrence.Outcome.Should().Be(OccurrenceOutcome.Posted);
        occurrence.TransactionId.Should().Be(transactionId);
        occurrence.ActualDate.Should().Be(actualDate);
        occurrence.ActualAmount.Should().Be(actualAmount);
    }

    [Fact]
    public void CreatePosted_WithLatePayment_ShouldSetLateOutcome()
    {
        // Arrange
        var patternId = 1;
        var expectedDate = new DateTime(2024, 2, 1);
        var actualDate = new DateTime(2024, 2, 5); // 4 days late
        var expectedAmount = 100m;
        var actualAmount = 100m;
        var transactionId = 123;

        // Act
        var occurrence = RecurringOccurrence.CreatePosted(
            patternId, expectedDate, expectedAmount,
            transactionId, actualDate, actualAmount);

        // Assert
        occurrence.Outcome.Should().Be(OccurrenceOutcome.Late);
    }

    [Fact]
    public void CreatePosted_WithEarlyPayment_ShouldSetPostedOutcome()
    {
        // Arrange
        var patternId = 1;
        var expectedDate = new DateTime(2024, 2, 5);
        var actualDate = new DateTime(2024, 2, 1); // 4 days early
        var expectedAmount = 100m;
        var actualAmount = 100m;
        var transactionId = 123;

        // Act
        var occurrence = RecurringOccurrence.CreatePosted(
            patternId, expectedDate, expectedAmount,
            transactionId, actualDate, actualAmount);

        // Assert
        occurrence.Outcome.Should().Be(OccurrenceOutcome.Posted);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void WasOnTime_WhenPosted_ShouldReturnTrue()
    {
        // Arrange
        var occurrence = new RecurringOccurrence { Outcome = OccurrenceOutcome.Posted };

        // Act & Assert
        occurrence.WasOnTime.Should().BeTrue();
    }

    [Fact]
    public void WasMissed_WhenMissed_ShouldReturnTrue()
    {
        // Arrange
        var occurrence = new RecurringOccurrence { Outcome = OccurrenceOutcome.Missed };

        // Act & Assert
        occurrence.WasMissed.Should().BeTrue();
    }

    [Fact]
    public void WasLate_WhenLate_ShouldReturnTrue()
    {
        // Arrange
        var occurrence = new RecurringOccurrence { Outcome = OccurrenceOutcome.Late };

        // Act & Assert
        occurrence.WasLate.Should().BeTrue();
    }

    #endregion

    #region Variance Calculation Tests

    [Fact]
    public void GetAmountVariance_WithActualAmount_ShouldReturnDifference()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedAmount = 100m,
            ActualAmount = 110m
        };

        // Act
        var variance = occurrence.GetAmountVariance();

        // Assert
        variance.Should().Be(10m);
    }

    [Fact]
    public void GetAmountVariance_WithNegativeVariance_ShouldReturnNegative()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedAmount = 100m,
            ActualAmount = 90m
        };

        // Act
        var variance = occurrence.GetAmountVariance();

        // Assert
        variance.Should().Be(-10m);
    }

    [Fact]
    public void GetAmountVariance_WithoutActualAmount_ShouldReturnNull()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedAmount = 100m,
            ActualAmount = null
        };

        // Act
        var variance = occurrence.GetAmountVariance();

        // Assert
        variance.Should().BeNull();
    }

    [Fact]
    public void GetAmountVariancePercentage_ShouldReturnCorrectPercentage()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedAmount = 100m,
            ActualAmount = 120m
        };

        // Act
        var percentage = occurrence.GetAmountVariancePercentage();

        // Assert
        percentage.Should().Be(20m); // 20% increase
    }

    [Fact]
    public void GetAmountVariancePercentage_WithZeroExpected_ShouldReturnNull()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedAmount = 0m,
            ActualAmount = 100m
        };

        // Act
        var percentage = occurrence.GetAmountVariancePercentage();

        // Assert
        percentage.Should().BeNull();
    }

    #endregion

    #region Days Late Calculation Tests

    [Fact]
    public void GetDaysLate_WithLatePayment_ShouldReturnPositiveDays()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedDate = new DateTime(2024, 2, 1),
            ActualDate = new DateTime(2024, 2, 5)
        };

        // Act
        var daysLate = occurrence.GetDaysLate();

        // Assert
        daysLate.Should().Be(4);
    }

    [Fact]
    public void GetDaysLate_WithEarlyPayment_ShouldReturnNegativeDays()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedDate = new DateTime(2024, 2, 5),
            ActualDate = new DateTime(2024, 2, 1)
        };

        // Act
        var daysLate = occurrence.GetDaysLate();

        // Assert
        daysLate.Should().Be(-4);
    }

    [Fact]
    public void GetDaysLate_WithOnTimePayment_ShouldReturnZero()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedDate = new DateTime(2024, 2, 1),
            ActualDate = new DateTime(2024, 2, 1)
        };

        // Act
        var daysLate = occurrence.GetDaysLate();

        // Assert
        daysLate.Should().Be(0);
    }

    [Fact]
    public void GetDaysLate_WithMissedPayment_ShouldReturnNull()
    {
        // Arrange
        var occurrence = new RecurringOccurrence
        {
            ExpectedDate = new DateTime(2024, 2, 1),
            ActualDate = null
        };

        // Act
        var daysLate = occurrence.GetDaysLate();

        // Assert
        daysLate.Should().BeNull();
    }

    #endregion
}
