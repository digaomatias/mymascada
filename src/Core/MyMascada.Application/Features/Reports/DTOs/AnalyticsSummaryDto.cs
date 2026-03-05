namespace MyMascada.Application.Features.Reports.DTOs;

/// <summary>
/// Response DTO for analytics summary across a configurable time period
/// </summary>
public class AnalyticsSummaryDto
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal AvgMonthlyIncome { get; set; }
    public decimal AvgMonthlyExpenses { get; set; }
    public decimal NetAmount { get; set; }
    public decimal SavingsRate { get; set; }
    public int MonthCount { get; set; }
    public MonthHighlightDto? BestMonth { get; set; }
    public MonthHighlightDto? WorstMonth { get; set; }
    public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();
    public List<YearlyComparisonDto> YearlyComparisons { get; set; } = new();
}

/// <summary>
/// Highlights a notable month (best or worst)
/// </summary>
public class MonthHighlightDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal NetAmount { get; set; }
}

/// <summary>
/// Monthly income, expenses, net, and savings rate
/// </summary>
public class MonthlyTrendDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net { get; set; }
    public decimal SavingsRate { get; set; }
}

/// <summary>
/// Yearly comparison of income, expenses, and savings
/// </summary>
public class YearlyComparisonDto
{
    public int Year { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetAmount { get; set; }
    public decimal SavingsRate { get; set; }
    public int MonthCount { get; set; }
}
