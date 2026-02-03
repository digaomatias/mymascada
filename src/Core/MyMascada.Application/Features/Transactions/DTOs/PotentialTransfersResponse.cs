namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// Response containing potential transfer matches and unmatched transactions
/// </summary>
public class PotentialTransfersResponse
{
    /// <summary>
    /// Groups of transactions that could be transfers
    /// </summary>
    public List<TransferGroupDto> TransferGroups { get; set; } = new();

    /// <summary>
    /// Transactions that look like transfers but have no match
    /// </summary>
    public List<UnmatchedTransferDto> UnmatchedTransfers { get; set; } = new();

    /// <summary>
    /// Total number of transfer groups found
    /// </summary>
    public int TotalGroups { get; set; }

    /// <summary>
    /// Total number of unmatched potential transfers
    /// </summary>
    public int TotalUnmatched { get; set; }

    /// <summary>
    /// When the analysis was processed
    /// </summary>
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A group of transactions that could form a transfer
/// </summary>
public class TransferGroupDto
{
    /// <summary>
    /// Unique identifier for this group
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Source transaction (outgoing)
    /// </summary>
    public TransactionDto SourceTransaction { get; set; } = null!;

    /// <summary>
    /// Destination transaction (incoming)
    /// </summary>
    public TransactionDto DestinationTransaction { get; set; } = null!;

    /// <summary>
    /// Confidence score for this match (0.0 to 1.0)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// The matching amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Date range of the transactions
    /// </summary>
    public string DateRange { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is already confirmed as a transfer
    /// </summary>
    public bool IsConfirmed { get; set; } = false;

    /// <summary>
    /// Reasons for the match
    /// </summary>
    public List<string> MatchReasons { get; set; } = new();
}

/// <summary>
/// A transaction that appears to be a transfer but has no match
/// </summary>
public class UnmatchedTransferDto
{
    /// <summary>
    /// The transaction that appears to be a transfer
    /// </summary>
    public TransactionDto Transaction { get; set; } = null!;

    /// <summary>
    /// Confidence that this is meant to be a transfer
    /// </summary>
    public decimal TransferConfidence { get; set; }

    /// <summary>
    /// Suggested destination account (if can be inferred)
    /// </summary>
    public int? SuggestedDestinationAccountId { get; set; }

    /// <summary>
    /// Suggested destination account name
    /// </summary>
    public string? SuggestedDestinationAccountName { get; set; }

    /// <summary>
    /// Reasons why this might be a transfer
    /// </summary>
    public List<string> TransferIndicators { get; set; } = new();
}

/// <summary>
/// Request to confirm transfer matches
/// </summary>
public class ConfirmTransferMatchRequest
{
    /// <summary>
    /// The transfer group ID to confirm
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>
    /// Source transaction ID
    /// </summary>
    public int SourceTransactionId { get; set; }

    /// <summary>
    /// Destination transaction ID
    /// </summary>
    public int DestinationTransactionId { get; set; }

    /// <summary>
    /// Optional description for the transfer
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional notes for the transfer
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Request to create a missing transfer transaction
/// </summary>
public class CreateMissingTransferRequest
{
    /// <summary>
    /// The existing transaction ID
    /// </summary>
    public int ExistingTransactionId { get; set; }

    /// <summary>
    /// The account ID for the missing transaction
    /// </summary>
    public int MissingAccountId { get; set; }

    /// <summary>
    /// Optional description for the transfer
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional notes for the transfer
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optional different date for the missing transaction
    /// </summary>
    public DateTime? TransactionDate { get; set; }
}

/// <summary>
/// Request to link two existing transactions as a transfer
/// </summary>
public class LinkTransactionsAsTransferRequest
{
    /// <summary>
    /// Source transaction ID (outgoing)
    /// </summary>
    public int SourceTransactionId { get; set; }

    /// <summary>
    /// Destination transaction ID (incoming)
    /// </summary>
    public int DestinationTransactionId { get; set; }

    /// <summary>
    /// Optional description for the transfer
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional notes for the transfer
    /// </summary>
    public string? Notes { get; set; }
}

/// <summary>
/// Bulk request to confirm multiple transfers
/// </summary>
public class BulkConfirmTransfersRequest
{
    /// <summary>
    /// List of transfer confirmations
    /// </summary>
    public List<ConfirmTransferMatchRequest> Confirmations { get; set; } = new();
}

/// <summary>
/// Response after confirming transfers
/// </summary>
public class ConfirmTransfersResponse
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Number of transfers successfully created
    /// </summary>
    public int TransfersCreated { get; set; }

    /// <summary>
    /// Number of transactions updated
    /// </summary>
    public int TransactionsUpdated { get; set; }

    /// <summary>
    /// Any errors that occurred
    /// </summary>
    public List<string> Errors { get; set; } = new();
}