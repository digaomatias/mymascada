namespace MyMascada.Application.Features.Wallets.DTOs;

public class WalletSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public decimal? TargetAmount { get; set; }
    public decimal Balance { get; set; }
    public int AllocationCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WalletDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsArchived { get; set; }
    public decimal? TargetAmount { get; set; }
    public decimal Balance { get; set; }
    public int AllocationCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<WalletAllocationDto> Allocations { get; set; } = new();
}

public class WalletAllocationDto
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public string TransactionDescription { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WalletDashboardSummaryDto
{
    public decimal TotalBalance { get; set; }
    public List<WalletSummaryDto> Wallets { get; set; } = new();
}

public class CreateWalletRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? TargetAmount { get; set; }
}

public class UpdateWalletRequest
{
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public string? Currency { get; set; }
    public bool? IsArchived { get; set; }
    public decimal? TargetAmount { get; set; }
}

public class CreateAllocationRequest
{
    public int TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
