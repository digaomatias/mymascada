namespace MyMascada.Application.Features.Reconciliation.DTOs;

/// <summary>
/// Request model for creating a reconciliation from Akahu bank data
/// </summary>
public record CreateAkahuReconciliationRequest
{
    /// <summary>
    /// The MyMascada account ID to reconcile
    /// </summary>
    public int AccountId { get; init; }

    /// <summary>
    /// Start date for fetching transactions from Akahu
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// End date for fetching transactions from Akahu
    /// </summary>
    public DateTime EndDate { get; init; }

    /// <summary>
    /// Optional statement end balance for comparison (if not provided, will use Akahu's current balance)
    /// </summary>
    public decimal? StatementEndBalance { get; init; }

    /// <summary>
    /// Optional notes for the reconciliation
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Response from creating an Akahu reconciliation
/// </summary>
public record AkahuReconciliationResponse
{
    /// <summary>
    /// The ID of the created reconciliation
    /// </summary>
    public int ReconciliationId { get; init; }

    /// <summary>
    /// Results from the automatic transaction matching
    /// </summary>
    public MatchingResultDto MatchingResult { get; init; } = new();

    /// <summary>
    /// Balance comparison between Akahu and MyMascada (if available)
    /// </summary>
    public AkahuBalanceComparisonDto? BalanceComparison { get; init; }
}

/// <summary>
/// Balance comparison data between Akahu and MyMascada
/// </summary>
public record AkahuBalanceComparisonDto
{
    /// <summary>
    /// Current balance from Akahu (note: this is current balance, not statement balance)
    /// </summary>
    public decimal AkahuBalance { get; init; }

    /// <summary>
    /// Calculated balance in MyMascada for the account
    /// </summary>
    public decimal MyMascadaBalance { get; init; }

    /// <summary>
    /// Difference between Akahu and MyMascada balances
    /// </summary>
    public decimal Difference { get; init; }

    /// <summary>
    /// Whether the balances are considered matched (within tolerance)
    /// </summary>
    public bool IsBalanced { get; init; }

    /// <summary>
    /// Indicates this is current account balance, not a statement balance
    /// </summary>
    public bool IsCurrentBalance { get; init; } = true;

    /// <summary>
    /// Total amount of pending transactions (included in Akahu balance but not in cleared transactions)
    /// </summary>
    public decimal PendingTransactionsTotal { get; init; }

    /// <summary>
    /// Number of pending transactions at the time of reconciliation
    /// </summary>
    public int PendingTransactionsCount { get; init; }
}

/// <summary>
/// Response indicating whether Akahu reconciliation is available for an account
/// </summary>
public record AkahuAvailabilityResponse
{
    /// <summary>
    /// Whether Akahu reconciliation is available for this account
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// The external Akahu account ID (if connected)
    /// </summary>
    public string? ExternalAccountId { get; init; }

    /// <summary>
    /// Reason why Akahu reconciliation is unavailable (if applicable)
    /// </summary>
    public string? UnavailableReason { get; init; }
}

/// <summary>
/// Request to import unmatched bank transactions as new MyMascada transactions
/// </summary>
public record ImportUnmatchedRequest
{
    /// <summary>
    /// Specific reconciliation item IDs to import (unmatched bank items)
    /// </summary>
    public IEnumerable<int>? ItemIds { get; init; }

    /// <summary>
    /// If true, import all unmatched bank transactions
    /// </summary>
    public bool ImportAll { get; init; }
}

/// <summary>
/// Result from importing unmatched transactions
/// </summary>
public record ImportUnmatchedResult
{
    /// <summary>
    /// Number of transactions successfully imported
    /// </summary>
    public int ImportedCount { get; init; }

    /// <summary>
    /// Number of items that were skipped (already imported or invalid)
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// IDs of the created transactions
    /// </summary>
    public IEnumerable<int> CreatedTransactionIds { get; init; } = new List<int>();

    /// <summary>
    /// Any errors encountered during import
    /// </summary>
    public IEnumerable<string> Errors { get; init; } = new List<string>();
}
