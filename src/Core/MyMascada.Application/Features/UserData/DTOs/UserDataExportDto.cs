namespace MyMascada.Application.Features.UserData.DTOs;

/// <summary>
/// Complete user data export for LGPD/GDPR compliance.
/// Contains all personal data associated with a user account.
/// </summary>
public class UserDataExportDto
{
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string ExportVersion { get; set; } = "1.0";

    // User Profile
    public UserProfileExportDto Profile { get; set; } = new();

    // Financial Data
    public List<AccountExportDto> Accounts { get; set; } = new();
    public List<TransactionExportDto> Transactions { get; set; } = new();
    public List<TransferExportDto> Transfers { get; set; } = new();
    public List<CategoryExportDto> Categories { get; set; } = new();
    public List<CategorizationRuleExportDto> CategorizationRules { get; set; } = new();

    // Bank Connections (without sensitive tokens)
    public List<BankConnectionExportDto> BankConnections { get; set; } = new();

    // Reconciliation History
    public List<ReconciliationExportDto> Reconciliations { get; set; } = new();

    // Activity Data
    public List<AuditLogExportDto> AuditLogs { get; set; } = new();

    // Summary
    public DataSummaryDto Summary { get; set; } = new();
}

public class UserProfileExportDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Currency { get; set; } = "NZD";
    public string TimeZone { get; set; } = "UTC";
    public string Locale { get; set; } = "en";
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
}

public class AccountExportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Currency { get; set; } = "NZD";
    public decimal CurrentBalance { get; set; }
    public string? Institution { get; set; }
    public string? LastFourDigits { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public int TransactionCount { get; set; }
}

public class TransactionExportDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? UserDescription { get; set; }
    public string? CategoryName { get; set; }
    public string? SubCategoryName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsReviewed { get; set; }
    public Guid? TransferId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TransferExportDto
{
    public int Id { get; set; }
    public string SourceAccountName { get; set; } = string.Empty;
    public string DestinationAccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransferDate { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CategoryExportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ParentCategoryName { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public bool IsSystemCategory { get; set; }
    public int TransactionCount { get; set; }
}

public class CategorizationRuleExportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Pattern { get; set; }
    public string? CategoryName { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public int ApplicationCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BankConnectionExportDto
{
    public int Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime ConnectedAt { get; set; }
    // Note: Tokens and credentials are NOT exported for security
}

public class ReconciliationExportDto
{
    public int Id { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public DateTime ReconciliationDate { get; set; }
    public DateTime StatementEndDate { get; set; }
    public decimal StatementEndBalance { get; set; }
    public decimal? CalculatedBalance { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AuditLogExportDto
{
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DataSummaryDto
{
    public int TotalAccounts { get; set; }
    public int TotalTransactions { get; set; }
    public int TotalTransfers { get; set; }
    public int TotalCategories { get; set; }
    public int TotalRules { get; set; }
    public int TotalReconciliations { get; set; }
    public DateTime? OldestTransactionDate { get; set; }
    public DateTime? NewestTransactionDate { get; set; }
}
