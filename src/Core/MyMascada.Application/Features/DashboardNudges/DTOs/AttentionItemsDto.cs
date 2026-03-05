namespace MyMascada.Application.Features.DashboardNudges.DTOs;

public class AttentionItemsDto
{
    public List<AttentionItemDto> Items { get; set; } = new();
}

public class AttentionItemDto
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int? Count { get; set; }
    public string? EntityName { get; set; }
    public decimal? Amount { get; set; }
    public int? DaysUntilDue { get; set; }
    public decimal? AnnualizedAmount { get; set; }
}
