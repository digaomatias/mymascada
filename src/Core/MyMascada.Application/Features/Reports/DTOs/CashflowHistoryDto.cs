namespace MyMascada.Application.Features.Reports.DTOs;

public class CashflowHistoryDto
{
    public List<CashflowMonthDto> Months { get; set; } = new();
}

public class CashflowMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
    public decimal Net { get; set; }
}
