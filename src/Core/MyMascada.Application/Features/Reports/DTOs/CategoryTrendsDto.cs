namespace MyMascada.Application.Features.Reports.DTOs;

/// <summary>
/// Response DTO for category spending trends over time
/// </summary>
public class CategoryTrendsResponseDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<CategoryTrendDto> Categories { get; set; } = new();
    public List<TrendPeriodSummaryDto> PeriodSummaries { get; set; } = new();
}

/// <summary>
/// Represents a single category's spending trend data
/// </summary>
public class CategoryTrendDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColor { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AverageMonthlySpent { get; set; }
    public List<PeriodAmountDto> Periods { get; set; } = new();
}

/// <summary>
/// Represents spending for a category in a specific time period
/// </summary>
public class PeriodAmountDto
{
    public DateTime PeriodStart { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Summary of all category spending in a period
/// </summary>
public class TrendPeriodSummaryDto
{
    public DateTime PeriodStart { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int TransactionCount { get; set; }
}
