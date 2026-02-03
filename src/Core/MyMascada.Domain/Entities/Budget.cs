using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a budget plan that tracks spending limits across categories.
/// Supports recurring monthly budgets and custom periods.
/// </summary>
public class Budget : BaseEntity
{
    /// <summary>
    /// Name of the budget (e.g., "Monthly Budget", "January 2025")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of this budget's purpose
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// User ID who owns this budget
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of budget period (Monthly, Weekly, Biweekly, Custom)
    /// </summary>
    public BudgetPeriodType PeriodType { get; set; } = BudgetPeriodType.Monthly;

    /// <summary>
    /// Start date of the budget period
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the budget period (null for recurring budgets)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Whether this budget automatically creates the next period when current ends
    /// </summary>
    public bool IsRecurring { get; set; } = true;

    /// <summary>
    /// Whether this budget is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties

    /// <summary>
    /// Category budget allocations for this budget
    /// </summary>
    public ICollection<BudgetCategory> BudgetCategories { get; set; } = new List<BudgetCategory>();

    /// <summary>
    /// Gets the end date for the current period, calculating it if not explicitly set
    /// </summary>
    public DateTime GetPeriodEndDate()
    {
        if (EndDate.HasValue)
            return EndDate.Value;

        return PeriodType switch
        {
            BudgetPeriodType.Weekly => StartDate.AddDays(7).AddSeconds(-1),
            BudgetPeriodType.Biweekly => StartDate.AddDays(14).AddSeconds(-1),
            BudgetPeriodType.Monthly => StartDate.AddMonths(1).AddSeconds(-1),
            BudgetPeriodType.Custom => throw new InvalidOperationException("Custom periods require an explicit end date"),
            _ => StartDate.AddMonths(1).AddSeconds(-1)
        };
    }

    /// <summary>
    /// Checks if a given date falls within this budget's period
    /// </summary>
    public bool ContainsDate(DateTime date)
    {
        var periodEnd = GetPeriodEndDate();
        return date >= StartDate && date <= periodEnd;
    }

    /// <summary>
    /// Gets the number of days remaining in the current budget period
    /// </summary>
    public int GetDaysRemaining()
    {
        var today = DateTimeProvider.UtcNow.Date;
        var periodEnd = GetPeriodEndDate().Date;

        if (today > periodEnd)
            return 0;

        return (periodEnd - today).Days + 1;
    }

    /// <summary>
    /// Gets the total number of days in the budget period
    /// </summary>
    public int GetTotalDays()
    {
        var periodEnd = GetPeriodEndDate().Date;
        return (periodEnd - StartDate.Date).Days + 1;
    }

    /// <summary>
    /// Gets the percentage of the period that has elapsed
    /// </summary>
    public decimal GetPeriodElapsedPercentage()
    {
        var totalDays = GetTotalDays();
        if (totalDays == 0) return 100m;

        var daysRemaining = GetDaysRemaining();
        var daysElapsed = totalDays - daysRemaining;

        return Math.Round((decimal)daysElapsed / totalDays * 100, 1);
    }

    /// <summary>
    /// Gets the total budgeted amount across all categories
    /// </summary>
    public decimal GetTotalBudgetedAmount()
    {
        return BudgetCategories
            .Where(bc => !bc.IsDeleted)
            .Sum(bc => bc.GetEffectiveBudget());
    }
}
