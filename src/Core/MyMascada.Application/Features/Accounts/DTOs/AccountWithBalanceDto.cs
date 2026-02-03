namespace MyMascada.Application.Features.Accounts.DTOs;

public class AccountWithBalanceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string? Institution { get; set; }
    public decimal CurrentBalance { get; set; } // Static balance from account
    public decimal CalculatedBalance { get; set; } // Real-time calculated balance from transactions
    public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}