using MyMascada.Application.Features.Accounts.DTOs;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.DataCleanup.DTOs;

/// <summary>
/// Response containing orphaned data analysis results
/// </summary>
public class OrphanedDataAnalysisDto
{
    public int OrphanedTransactionCount { get; set; }
    public List<OrphanedAccountDto> OrphanedAccounts { get; set; } = new();
    public List<TransactionDto> OrphanedTransactions { get; set; } = new();
    public DateTime AnalysisTimestamp { get; set; }
    public bool HasOrphanedData => OrphanedTransactionCount > 0;
}

/// <summary>
/// Represents an account that was soft-deleted but still has transactions
/// </summary>
public class OrphanedAccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? LastFourDigits { get; set; }
    public int TransactionCount { get; set; }
    public DateTime? DeletedAt { get; set; }
    public decimal TotalTransactionAmount { get; set; }
    public List<TransactionDto> Transactions { get; set; } = new();
}

/// <summary>
/// Request for moving transactions from one account to another
/// </summary>
public class MoveTransactionsRequest
{
    public int SourceAccountId { get; set; }
    public int DestinationAccountId { get; set; }
    public List<int>? TransactionIds { get; set; } // If null, move all transactions
}

/// <summary>
/// Request for restoring a soft-deleted account
/// </summary>
public class RestoreAccountRequest
{
    public int AccountId { get; set; }
    public bool RestoreAsActive { get; set; } = true;
}

/// <summary>
/// Request for hard deleting orphaned transactions
/// </summary>
public class HardDeleteTransactionsRequest
{
    public int AccountId { get; set; }
    public List<int>? TransactionIds { get; set; } // If null, delete all transactions for the account
    public bool ConfirmHardDelete { get; set; } = false;
}

/// <summary>
/// Response for cleanup operations
/// </summary>
public class CleanupOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Response containing available accounts for move operations
/// </summary>
public class AvailableAccountsForMoveDto
{
    public List<AccountDto> ActiveAccounts { get; set; } = new();
    public OrphanedAccountDto OrphanedAccount { get; set; } = new();
}

/// <summary>
/// Summary of data integrity issues
/// </summary>
public class DataIntegritySummaryDto
{
    public int OrphanedTransactionCount { get; set; }
    public int OrphanedAccountCount { get; set; }
    public int TotalAffectedTransactions { get; set; }
    public decimal TotalAffectedAmount { get; set; }
    public DateTime LastAnalysis { get; set; }
    public bool RequiresCleanup => OrphanedTransactionCount > 0 || OrphanedAccountCount > 0;
    public List<DataIntegrityIssue> Issues { get; set; } = new();
}

/// <summary>
/// Represents a specific data integrity issue
/// </summary>
public class DataIntegrityIssue
{
    public string Type { get; set; } = string.Empty; // "OrphanedTransactions", "SoftDeletedAccount", etc.
    public string Description { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Severity { get; set; } = string.Empty; // "Low", "Medium", "High", "Critical"
    public Dictionary<string, object> Details { get; set; } = new();
}