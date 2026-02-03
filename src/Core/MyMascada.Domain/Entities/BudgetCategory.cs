using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a budget allocation for a specific category within a budget.
/// Links budgets to categories with amount limits and rollover settings.
/// </summary>
public class BudgetCategory : BaseEntity
{
    /// <summary>
    /// The budget this allocation belongs to
    /// </summary>
    public int BudgetId { get; set; }

    /// <summary>
    /// The category being budgeted
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// The budgeted amount for this category (positive value)
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Budgeted amount must be positive")]
    public decimal BudgetedAmount { get; set; }

    /// <summary>
    /// Amount rolled over from the previous period (can be positive or negative based on settings)
    /// </summary>
    public decimal? RolloverAmount { get; set; }

    /// <summary>
    /// Whether unused budget should carry forward to the next period
    /// </summary>
    public bool AllowRollover { get; set; } = false;

    /// <summary>
    /// Whether overspending should carry forward as debt (only applies if AllowRollover is true)
    /// </summary>
    public bool CarryOverspend { get; set; } = false;

    /// <summary>
    /// Whether to include transactions from subcategories in the budget calculation
    /// </summary>
    public bool IncludeSubcategories { get; set; } = true;

    /// <summary>
    /// Optional notes about this budget allocation
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent budget
    /// </summary>
    public Budget Budget { get; set; } = null!;

    /// <summary>
    /// The category being budgeted
    /// </summary>
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Gets the effective budget including any rollover amount
    /// </summary>
    public decimal GetEffectiveBudget()
    {
        return BudgetedAmount + (RolloverAmount ?? 0);
    }

    /// <summary>
    /// Calculates the remaining budget given the actual spending
    /// </summary>
    /// <param name="actualSpending">The total spent in this category (as a positive value)</param>
    public decimal GetRemainingBudget(decimal actualSpending)
    {
        return GetEffectiveBudget() - actualSpending;
    }

    /// <summary>
    /// Calculates what percentage of the budget has been used
    /// </summary>
    /// <param name="actualSpending">The total spent in this category (as a positive value)</param>
    public decimal GetUsedPercentage(decimal actualSpending)
    {
        var effective = GetEffectiveBudget();
        if (effective == 0) return actualSpending > 0 ? 100m : 0m;

        return Math.Round(actualSpending / effective * 100, 1);
    }

    /// <summary>
    /// Determines if the category is over budget
    /// </summary>
    /// <param name="actualSpending">The total spent in this category (as a positive value)</param>
    public bool IsOverBudget(decimal actualSpending)
    {
        return actualSpending > GetEffectiveBudget();
    }

    /// <summary>
    /// Determines if the category is approaching the budget limit (>80% used)
    /// </summary>
    /// <param name="actualSpending">The total spent in this category (as a positive value)</param>
    public bool IsApproachingLimit(decimal actualSpending)
    {
        var usedPercentage = GetUsedPercentage(actualSpending);
        return usedPercentage >= 80m && usedPercentage < 100m;
    }

    /// <summary>
    /// Calculates the rollover amount for the next period
    /// </summary>
    /// <param name="actualSpending">The total spent in this category (as a positive value)</param>
    public decimal? CalculateNextPeriodRollover(decimal actualSpending)
    {
        if (!AllowRollover) return null;

        var remaining = GetRemainingBudget(actualSpending);

        // If we're under budget, always carry forward the positive remainder
        if (remaining >= 0) return remaining;

        // If we're over budget, only carry forward if CarryOverspend is enabled
        return CarryOverspend ? remaining : 0;
    }
}
