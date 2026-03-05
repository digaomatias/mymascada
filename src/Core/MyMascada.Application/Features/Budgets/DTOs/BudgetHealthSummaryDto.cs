namespace MyMascada.Application.Features.Budgets.DTOs;

public class BudgetHealthSummaryDto
{
    public decimal TotalBudgeted { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal OverallPercentage { get; set; }
    public int BudgetsOverLimit { get; set; }
    public int BudgetsApproaching { get; set; }
    public int OverCount { get; set; }
    public int AtRiskCount { get; set; }
    public int OnTrackCount { get; set; }
    public int InactiveCount { get; set; }
    public int? NearestDeadlineDays { get; set; }
    public List<BudgetRiskItemDto> Items { get; set; } = new();
}

public class BudgetRiskItemDto
{
    public int BudgetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RiskState { get; set; } = string.Empty;
    public int PriorityScore { get; set; }
    public decimal ExpectedSpent { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercentage { get; set; }
    public bool IsOverspendingPace { get; set; }
}
