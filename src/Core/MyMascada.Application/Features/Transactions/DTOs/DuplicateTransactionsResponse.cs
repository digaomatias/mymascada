namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// Response containing grouped duplicate transactions
/// </summary>
public class DuplicateTransactionsResponse
{
    public List<DuplicateGroupDto> DuplicateGroups { get; set; } = new();
    public int TotalGroups { get; set; }
    public int TotalTransactions { get; set; }
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// A group of potentially duplicate transactions
/// </summary>
public class DuplicateGroupDto
{
    public Guid Id { get; set; }
    public List<TransactionDto> Transactions { get; set; } = new();
    public decimal HighestConfidence { get; set; }
    public decimal TotalAmount { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Request to resolve duplicate transactions
/// </summary>
public class ResolveDuplicatesRequest
{
    public Guid GroupId { get; set; }
    public List<int> TransactionIdsToKeep { get; set; } = new();
    public List<int> TransactionIdsToDelete { get; set; } = new();
    public bool MarkAsNotDuplicate { get; set; } = false;
    public string? Notes { get; set; }
}

/// <summary>
/// Response after resolving duplicates
/// </summary>
public class ResolveDuplicatesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TransactionsDeleted { get; set; }
    public int TransactionsKept { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Bulk resolution request for multiple duplicate groups
/// </summary>
public class BulkResolveDuplicatesRequest
{
    public List<ResolveDuplicatesRequest> Resolutions { get; set; } = new();
}

/// <summary>
/// Request to bulk delete multiple transactions
/// </summary>
public class BulkDeleteTransactionsRequest
{
    public List<int> TransactionIds { get; set; } = new();
    public string? Reason { get; set; }
}

/// <summary>
/// Response after bulk deleting transactions
/// </summary>
public class BulkDeleteTransactionsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TransactionsDeleted { get; set; }
    public List<string> Errors { get; set; } = new();
}