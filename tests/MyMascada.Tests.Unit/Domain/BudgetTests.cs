using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Domain;

public class BudgetTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        var budget = new Budget();

        // Assert
        budget.Id.Should().Be(0);
        budget.Name.Should().BeEmpty();
        budget.PeriodType.Should().Be(BudgetPeriodType.Monthly);
        budget.IsRecurring.Should().BeTrue();
        budget.IsActive.Should().BeTrue();
        budget.IsDeleted.Should().BeFalse();
        budget.BudgetCategories.Should().BeEmpty();
    }

    [Fact]
    public void SetProperties_ShouldUpdateCorrectly()
    {
        // Arrange
        var budget = new Budget();
        var userId = Guid.NewGuid();
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        budget.Name = "January 2025 Budget";
        budget.Description = "Monthly household budget";
        budget.UserId = userId;
        budget.PeriodType = BudgetPeriodType.Monthly;
        budget.StartDate = startDate;
        budget.IsRecurring = true;

        // Assert
        budget.Name.Should().Be("January 2025 Budget");
        budget.Description.Should().Be("Monthly household budget");
        budget.UserId.Should().Be(userId);
        budget.PeriodType.Should().Be(BudgetPeriodType.Monthly);
        budget.StartDate.Should().Be(startDate);
        budget.IsRecurring.Should().BeTrue();
    }

    [Theory]
    [InlineData(BudgetPeriodType.Monthly)]
    [InlineData(BudgetPeriodType.Weekly)]
    [InlineData(BudgetPeriodType.Biweekly)]
    [InlineData(BudgetPeriodType.Custom)]
    public void PeriodType_ShouldAcceptAllValidValues(BudgetPeriodType periodType)
    {
        // Arrange
        var budget = new Budget();

        // Act
        budget.PeriodType = periodType;

        // Assert
        budget.PeriodType.Should().Be(periodType);
    }

    [Fact]
    public void GetPeriodEndDate_WithMonthlyPeriod_ShouldReturnEndOfMonth()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var endDate = budget.GetPeriodEndDate();

        // Assert
        endDate.Year.Should().Be(2025);
        endDate.Month.Should().Be(1);
        endDate.Day.Should().Be(31);
    }

    [Fact]
    public void GetPeriodEndDate_WithWeeklyPeriod_ShouldReturnOneWeekLater()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Weekly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var endDate = budget.GetPeriodEndDate();

        // Assert
        endDate.Year.Should().Be(2025);
        endDate.Month.Should().Be(1);
        endDate.Day.Should().Be(7);
    }

    [Fact]
    public void GetPeriodEndDate_WithBiweeklyPeriod_ShouldReturnTwoWeeksLater()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Biweekly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var endDate = budget.GetPeriodEndDate();

        // Assert
        endDate.Year.Should().Be(2025);
        endDate.Month.Should().Be(1);
        endDate.Day.Should().Be(14);
    }

    [Fact]
    public void GetPeriodEndDate_WithExplicitEndDate_ShouldReturnExplicitDate()
    {
        // Arrange
        var explicitEnd = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Custom,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = explicitEnd
        };

        // Act
        var endDate = budget.GetPeriodEndDate();

        // Assert
        endDate.Should().Be(explicitEnd);
    }

    [Fact]
    public void GetPeriodEndDate_WithCustomPeriodNoEndDate_ShouldThrowException()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Custom,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = null
        };

        // Act & Assert
        budget.Invoking(b => b.GetPeriodEndDate())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Custom periods require an explicit end date*");
    }

    [Fact]
    public void ContainsDate_MiddleOfPeriod_ShouldReturnTrue()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act & Assert
        budget.ContainsDate(new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
    }

    [Fact]
    public void ContainsDate_StartOfPeriod_ShouldReturnTrue()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act & Assert
        budget.ContainsDate(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Should().BeTrue();
    }

    [Fact]
    public void ContainsDate_EndOfPeriod_ShouldReturnTrue()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act & Assert
        budget.ContainsDate(new DateTime(2025, 1, 31, 23, 59, 58, DateTimeKind.Utc)).Should().BeTrue();
    }

    [Fact]
    public void ContainsDate_BeforePeriod_ShouldReturnFalse()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act & Assert
        budget.ContainsDate(new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc)).Should().BeFalse();
    }

    [Fact]
    public void ContainsDate_AfterPeriod_ShouldReturnFalse()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act & Assert
        budget.ContainsDate(new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)).Should().BeFalse();
    }

    [Fact]
    public void GetTotalDays_ForMonthlyBudget_ShouldReturnCorrectDays()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Monthly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var totalDays = budget.GetTotalDays();

        // Assert
        totalDays.Should().Be(31); // January has 31 days
    }

    [Fact]
    public void GetTotalDays_ForWeeklyBudget_ShouldReturn7()
    {
        // Arrange
        var budget = new Budget
        {
            PeriodType = BudgetPeriodType.Weekly,
            StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var totalDays = budget.GetTotalDays();

        // Assert
        totalDays.Should().Be(7);
    }

    [Fact]
    public void GetTotalBudgetedAmount_WithNoCategories_ShouldReturnZero()
    {
        // Arrange
        var budget = new Budget();

        // Act
        var total = budget.GetTotalBudgetedAmount();

        // Assert
        total.Should().Be(0);
    }

    [Fact]
    public void GetTotalBudgetedAmount_WithCategories_ShouldSumAllAmounts()
    {
        // Arrange
        var budget = new Budget
        {
            BudgetCategories = new List<BudgetCategory>
            {
                new() { BudgetedAmount = 500m },
                new() { BudgetedAmount = 300m },
                new() { BudgetedAmount = 200m }
            }
        };

        // Act
        var total = budget.GetTotalBudgetedAmount();

        // Assert
        total.Should().Be(1000m);
    }

    [Fact]
    public void GetTotalBudgetedAmount_WithRolloverAmounts_ShouldIncludeRollovers()
    {
        // Arrange
        var budget = new Budget
        {
            BudgetCategories = new List<BudgetCategory>
            {
                new() { BudgetedAmount = 500m, RolloverAmount = 50m },
                new() { BudgetedAmount = 300m, RolloverAmount = -20m }, // Overspent last month
                new() { BudgetedAmount = 200m, RolloverAmount = null }
            }
        };

        // Act
        var total = budget.GetTotalBudgetedAmount();

        // Assert
        total.Should().Be(1030m); // 550 + 280 + 200
    }

    [Fact]
    public void GetTotalBudgetedAmount_ShouldExcludeDeletedCategories()
    {
        // Arrange
        var budget = new Budget
        {
            BudgetCategories = new List<BudgetCategory>
            {
                new() { BudgetedAmount = 500m, IsDeleted = false },
                new() { BudgetedAmount = 300m, IsDeleted = true },
                new() { BudgetedAmount = 200m, IsDeleted = false }
            }
        };

        // Act
        var total = budget.GetTotalBudgetedAmount();

        // Assert
        total.Should().Be(700m); // Only non-deleted: 500 + 200
    }

    [Fact]
    public void AuditFields_ShouldBeSettable()
    {
        // Arrange
        var budget = new Budget();
        var now = DateTime.UtcNow;

        // Act
        budget.CreatedAt = now;
        budget.UpdatedAt = now;

        // Assert
        budget.CreatedAt.Should().Be(now);
        budget.UpdatedAt.Should().Be(now);
    }
}
