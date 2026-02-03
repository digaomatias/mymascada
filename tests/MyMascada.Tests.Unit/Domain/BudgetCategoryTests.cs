using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Domain;

public class BudgetCategoryTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var budgetCategory = new BudgetCategory();

        // Assert
        budgetCategory.Id.Should().Be(0);
        budgetCategory.BudgetedAmount.Should().Be(0);
        budgetCategory.RolloverAmount.Should().BeNull();
        budgetCategory.AllowRollover.Should().BeFalse();
        budgetCategory.CarryOverspend.Should().BeFalse();
        budgetCategory.IncludeSubcategories.Should().BeTrue();
        budgetCategory.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void SetProperties_ShouldUpdateCorrectly()
    {
        // Arrange
        var budgetCategory = new BudgetCategory();

        // Act
        budgetCategory.BudgetId = 1;
        budgetCategory.CategoryId = 5;
        budgetCategory.BudgetedAmount = 500m;
        budgetCategory.AllowRollover = true;
        budgetCategory.CarryOverspend = true;
        budgetCategory.IncludeSubcategories = false;
        budgetCategory.Notes = "Includes dining out and groceries";

        // Assert
        budgetCategory.BudgetId.Should().Be(1);
        budgetCategory.CategoryId.Should().Be(5);
        budgetCategory.BudgetedAmount.Should().Be(500m);
        budgetCategory.AllowRollover.Should().BeTrue();
        budgetCategory.CarryOverspend.Should().BeTrue();
        budgetCategory.IncludeSubcategories.Should().BeFalse();
        budgetCategory.Notes.Should().Be("Includes dining out and groceries");
    }

    [Fact]
    public void GetEffectiveBudget_WithNoRollover_ShouldReturnBudgetedAmount()
    {
        // Arrange
        var budgetCategory = new BudgetCategory { BudgetedAmount = 500m };

        // Act
        var effective = budgetCategory.GetEffectiveBudget();

        // Assert
        effective.Should().Be(500m);
    }

    [Fact]
    public void GetEffectiveBudget_WithPositiveRollover_ShouldAddRollover()
    {
        // Arrange
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            RolloverAmount = 75m
        };

        // Act
        var effective = budgetCategory.GetEffectiveBudget();

        // Assert
        effective.Should().Be(575m);
    }

    [Fact]
    public void GetEffectiveBudget_WithNegativeRollover_ShouldSubtractRollover()
    {
        // Arrange - Overspent last month by $50
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            RolloverAmount = -50m
        };

        // Act
        var effective = budgetCategory.GetEffectiveBudget();

        // Assert
        effective.Should().Be(450m);
    }

    [Theory]
    [InlineData(500, 250, 250)]   // 50% used, 50% remaining
    [InlineData(500, 500, 0)]     // 100% used, nothing remaining
    [InlineData(500, 600, -100)]  // 120% used, over budget
    [InlineData(500, 0, 500)]     // Nothing used, full budget remaining
    public void GetRemainingBudget_ShouldCalculateCorrectly(decimal budgeted, decimal spent, decimal expected)
    {
        // Arrange
        var budgetCategory = new BudgetCategory { BudgetedAmount = budgeted };

        // Act
        var remaining = budgetCategory.GetRemainingBudget(spent);

        // Assert
        remaining.Should().Be(expected);
    }

    [Theory]
    [InlineData(500, 250, 50.0)]   // 50% used
    [InlineData(500, 500, 100.0)]  // 100% used
    [InlineData(500, 600, 120.0)]  // 120% used (over budget)
    [InlineData(500, 0, 0.0)]      // 0% used
    [InlineData(500, 400, 80.0)]   // 80% used (approaching limit)
    public void GetUsedPercentage_ShouldCalculateCorrectly(decimal budgeted, decimal spent, decimal expected)
    {
        // Arrange
        var budgetCategory = new BudgetCategory { BudgetedAmount = budgeted };

        // Act
        var percentage = budgetCategory.GetUsedPercentage(spent);

        // Assert
        percentage.Should().Be(expected);
    }

    [Fact]
    public void GetUsedPercentage_WithZeroBudget_AndZeroSpending_ShouldReturnZero()
    {
        // Arrange
        var budgetCategory = new BudgetCategory { BudgetedAmount = 0m };

        // Act
        var percentage = budgetCategory.GetUsedPercentage(0m);

        // Assert
        percentage.Should().Be(0m);
    }

    [Fact]
    public void GetUsedPercentage_WithZeroBudget_AndSomeSpending_ShouldReturn100()
    {
        // Arrange
        var budgetCategory = new BudgetCategory { BudgetedAmount = 0m };

        // Act
        var percentage = budgetCategory.GetUsedPercentage(50m);

        // Assert
        percentage.Should().Be(100m);
    }

    [Theory]
    [InlineData(500, 250, false)]  // 50% - not over
    [InlineData(500, 500, false)]  // 100% - exactly at limit (not over)
    [InlineData(500, 500.01, true)] // Slightly over
    [InlineData(500, 600, true)]   // 120% - over budget
    public void IsOverBudget_ShouldReturnCorrectResult(decimal budgeted, decimal spent, bool expected)
    {
        // Arrange
        var budgetCategory = new BudgetCategory { BudgetedAmount = budgeted };

        // Act
        var isOver = budgetCategory.IsOverBudget(spent);

        // Assert
        isOver.Should().Be(expected);
    }

    [Theory]
    [InlineData(500, 399, false)]  // 79.8% - not approaching
    [InlineData(500, 400, true)]   // 80% - approaching
    [InlineData(500, 450, true)]   // 90% - approaching
    [InlineData(500, 499, true)]   // 99.8% - approaching
    [InlineData(500, 500, false)]  // 100% - at limit (not "approaching")
    [InlineData(500, 550, false)]  // 110% - over budget (not "approaching")
    public void IsApproachingLimit_ShouldReturnCorrectResult(decimal budgeted, decimal spent, bool expected)
    {
        // Arrange
        var budgetCategory = new BudgetCategory { BudgetedAmount = budgeted };

        // Act
        var isApproaching = budgetCategory.IsApproachingLimit(spent);

        // Assert
        isApproaching.Should().Be(expected);
    }

    [Fact]
    public void CalculateNextPeriodRollover_WhenRolloverDisabled_ShouldReturnNull()
    {
        // Arrange
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            AllowRollover = false
        };

        // Act
        var rollover = budgetCategory.CalculateNextPeriodRollover(300m);

        // Assert
        rollover.Should().BeNull();
    }

    [Fact]
    public void CalculateNextPeriodRollover_WhenUnderBudget_ShouldReturnPositiveRollover()
    {
        // Arrange - Spent $300 of $500, $200 remaining
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            AllowRollover = true
        };

        // Act
        var rollover = budgetCategory.CalculateNextPeriodRollover(300m);

        // Assert
        rollover.Should().Be(200m);
    }

    [Fact]
    public void CalculateNextPeriodRollover_WhenOverBudget_AndCarryOverspendDisabled_ShouldReturnZero()
    {
        // Arrange - Spent $600 of $500, $100 over
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            AllowRollover = true,
            CarryOverspend = false
        };

        // Act
        var rollover = budgetCategory.CalculateNextPeriodRollover(600m);

        // Assert
        rollover.Should().Be(0m);
    }

    [Fact]
    public void CalculateNextPeriodRollover_WhenOverBudget_AndCarryOverspendEnabled_ShouldReturnNegativeRollover()
    {
        // Arrange - Spent $600 of $500, $100 over
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            AllowRollover = true,
            CarryOverspend = true
        };

        // Act
        var rollover = budgetCategory.CalculateNextPeriodRollover(600m);

        // Assert
        rollover.Should().Be(-100m);
    }

    [Fact]
    public void CalculateNextPeriodRollover_WithExistingRollover_ShouldIncludeInCalculation()
    {
        // Arrange - $500 budget + $50 rollover = $550 effective, spent $400
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            RolloverAmount = 50m,
            AllowRollover = true
        };

        // Act
        var rollover = budgetCategory.CalculateNextPeriodRollover(400m);

        // Assert
        rollover.Should().Be(150m); // $550 - $400 = $150
    }

    [Fact]
    public void AuditFields_ShouldBeSettable()
    {
        // Arrange
        var budgetCategory = new BudgetCategory();
        var now = DateTime.UtcNow;

        // Act
        budgetCategory.CreatedAt = now;
        budgetCategory.UpdatedAt = now;

        // Assert
        budgetCategory.CreatedAt.Should().Be(now);
        budgetCategory.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void GetUsedPercentage_WithRollover_ShouldUseEffectiveBudget()
    {
        // Arrange - $500 budget + $100 rollover = $600 effective
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            RolloverAmount = 100m
        };

        // Act - Spent $300 of $600 effective = 50%
        var percentage = budgetCategory.GetUsedPercentage(300m);

        // Assert
        percentage.Should().Be(50m);
    }

    [Fact]
    public void IsOverBudget_WithRollover_ShouldUseEffectiveBudget()
    {
        // Arrange - $500 budget + $100 rollover = $600 effective
        var budgetCategory = new BudgetCategory
        {
            BudgetedAmount = 500m,
            RolloverAmount = 100m
        };

        // Act & Assert - $550 spent is under $600 effective
        budgetCategory.IsOverBudget(550m).Should().BeFalse();

        // $650 spent is over $600 effective
        budgetCategory.IsOverBudget(650m).Should().BeTrue();
    }
}
