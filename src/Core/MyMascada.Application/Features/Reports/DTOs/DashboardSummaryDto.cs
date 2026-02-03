namespace MyMascada.Application.Features.Reports.DTOs;

public class DashboardSummaryDto
{
    public decimal TotalBalance { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public int TransactionCount { get; set; }
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}

public class RecentTransactionDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? UserDescription { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
}

public class AccountBalanceReportDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public DateTime? LastTransactionDate { get; set; }
}

public class MonthlySummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetAmount { get; set; }
    public int TransactionCount { get; set; }
    public List<CategorySpendingDto> TopCategories { get; set; } = new();
}

public class CategorySpendingDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColor { get; set; }
    public decimal Amount { get; set; }
    public int TransactionCount { get; set; }
    public decimal Percentage { get; set; }
}