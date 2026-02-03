namespace MyMascada.Application.Features.Budgets.DTOs;

/// <summary>
/// Summary of a budget for list views
/// </summary>
public class BudgetSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsRecurring { get; set; }
    public bool IsActive { get; set; }
    public int CategoryCount { get; set; }
    public decimal TotalBudgeted { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalRemaining { get; set; }
    public decimal UsedPercentage { get; set; }
    public int DaysRemaining { get; set; }
    public bool IsCurrentPeriod { get; set; }
}

/// <summary>
/// Detailed budget with all category allocations
/// </summary>
public class BudgetDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsRecurring { get; set; }
    public bool IsActive { get; set; }
    public decimal TotalBudgeted { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalRemaining { get; set; }
    public decimal UsedPercentage { get; set; }
    public int DaysRemaining { get; set; }
    public int TotalDays { get; set; }
    public decimal PeriodElapsedPercentage { get; set; }
    public List<BudgetCategoryProgressDto> Categories { get; set; } = new();
}

/// <summary>
/// Progress for a single category within a budget
/// </summary>
public class BudgetCategoryProgressDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColor { get; set; }
    public string? CategoryIcon { get; set; }
    public string? ParentCategoryName { get; set; }
    public decimal BudgetedAmount { get; set; }
    public decimal RolloverAmount { get; set; }
    public decimal EffectiveBudget { get; set; }
    public decimal ActualSpent { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal UsedPercentage { get; set; }
    public bool IsOverBudget { get; set; }
    public bool IsApproachingLimit { get; set; }
    public int TransactionCount { get; set; }
    public bool AllowRollover { get; set; }
    public bool IncludeSubcategories { get; set; }

    /// <summary>
    /// Budget status: OnTrack, Approaching, Over
    /// </summary>
    public string Status => IsOverBudget ? "Over" : IsApproachingLimit ? "Approaching" : "OnTrack";
}

/// <summary>
/// Suggested budget based on historical spending
/// </summary>
public class BudgetSuggestionDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColor { get; set; }
    public string? CategoryIcon { get; set; }
    public string? ParentCategoryName { get; set; }
    public decimal AverageMonthlySpending { get; set; }
    public decimal SuggestedBudget { get; set; }
    public decimal MinSpending { get; set; }
    public decimal MaxSpending { get; set; }
    public int MonthsAnalyzed { get; set; }
    public int TotalTransactionCount { get; set; }

    /// <summary>
    /// Confidence level based on spending consistency (0-1)
    /// </summary>
    public decimal Confidence { get; set; }

    // Enhanced suggestion fields

    /// <summary>
    /// Spending trend direction: Increasing, Decreasing, Stable
    /// </summary>
    public string SpendingTrend { get; set; } = "Stable";

    /// <summary>
    /// Month-over-month change percentage (positive = increasing)
    /// </summary>
    public decimal TrendPercentage { get; set; }

    /// <summary>
    /// Projected spending for next month based on trend
    /// </summary>
    public decimal ProjectedNextMonth { get; set; }

    /// <summary>
    /// Priority score for budgeting (0-100, higher = more important to budget)
    /// </summary>
    public int PriorityScore { get; set; }

    /// <summary>
    /// Recommendation type: Essential, Regular, Discretionary, SavingsOpportunity
    /// </summary>
    public string RecommendationType { get; set; } = "Regular";

    /// <summary>
    /// Human-readable insight about the spending pattern
    /// </summary>
    public string? Insight { get; set; }

    /// <summary>
    /// Percentage of total expenses this category represents
    /// </summary>
    public decimal PercentageOfTotal { get; set; }

    /// <summary>
    /// Most recent month's spending
    /// </summary>
    public decimal LastMonthSpending { get; set; }
}

/// <summary>
/// Request to create a new budget
/// </summary>
public class CreateBudgetRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PeriodType { get; set; } = "Monthly";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; } = true;
    public List<CreateBudgetCategoryRequest> Categories { get; set; } = new();
}

/// <summary>
/// Request to create a category allocation in a budget
/// </summary>
public class CreateBudgetCategoryRequest
{
    public int CategoryId { get; set; }
    public decimal BudgetedAmount { get; set; }
    public bool AllowRollover { get; set; } = false;
    public bool CarryOverspend { get; set; } = false;
    public bool IncludeSubcategories { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>
/// Request to update an existing budget
/// </summary>
public class UpdateBudgetRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsRecurring { get; set; }
}

/// <summary>
/// Request to update a category allocation
/// </summary>
public class UpdateBudgetCategoryRequest
{
    public decimal? BudgetedAmount { get; set; }
    public bool? AllowRollover { get; set; }
    public bool? CarryOverspend { get; set; }
    public bool? IncludeSubcategories { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Summary of spending by category for a period
/// </summary>
public class CategorySpendingSummaryDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColor { get; set; }
    public decimal TotalSpent { get; set; }
    public int TransactionCount { get; set; }
    public List<int> IncludedCategoryIds { get; set; } = new();
}

/// <summary>
/// Result of processing budget rollovers
/// </summary>
public class BudgetRolloverResultDto
{
    public DateTime ProcessedAt { get; set; }
    public bool PreviewOnly { get; set; }
    public int TotalBudgetsProcessed { get; set; }
    public int NewBudgetsCreated { get; set; }
    public decimal TotalRolloverAmount { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<BudgetRolloverDto> ProcessedBudgets { get; set; } = new();
}

/// <summary>
/// Details of a single budget rollover
/// </summary>
public class BudgetRolloverDto
{
    public int SourceBudgetId { get; set; }
    public string SourceBudgetName { get; set; } = string.Empty;
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public bool IsRecurring { get; set; }
    public decimal TotalRollover { get; set; }
    public bool NewBudgetCreated { get; set; }
    public int? NewBudgetId { get; set; }
    public DateTime? NewPeriodStartDate { get; set; }
    public DateTime? NewPeriodEndDate { get; set; }
    public List<CategoryRolloverDto> CategoryRollovers { get; set; } = new();
}

/// <summary>
/// Rollover details for a single category
/// </summary>
public class CategoryRolloverDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal BudgetedAmount { get; set; }
    public decimal ActualSpent { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal RolloverAmount { get; set; }
    public bool CarryOverspend { get; set; }

    /// <summary>
    /// Status: Surplus (positive rollover) or Deficit (negative rollover)
    /// </summary>
    public string Status => RolloverAmount >= 0 ? "Surplus" : "Deficit";
}

/// <summary>
/// Budget alert for notifications
/// </summary>
public class BudgetAlertDto
{
    public int BudgetId { get; set; }
    public string BudgetName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public BudgetAlertType AlertType { get; set; }
    public decimal PercentUsed { get; set; }
    public decimal BudgetedAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime AlertDate { get; set; }
}

/// <summary>
/// Type of budget alert
/// </summary>
public enum BudgetAlertType
{
    /// <summary>
    /// Spending has reached the alert threshold
    /// </summary>
    NearLimit = 1,

    /// <summary>
    /// Spending has exceeded the budget
    /// </summary>
    OverBudget = 2,

    /// <summary>
    /// Budget period is ending soon with significant remaining
    /// </summary>
    PeriodEnding = 3,

    /// <summary>
    /// Spending pace suggests overspending by period end
    /// </summary>
    PaceWarning = 4
}
