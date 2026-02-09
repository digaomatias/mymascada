namespace MyMascada.Application.Features.BankConnections.DTOs;

/// <summary>
/// Configuration passed to bank providers for connection operations.
/// Contains credentials and settings needed to communicate with the provider.
/// </summary>
public record BankConnectionConfig
{
    /// <summary>
    /// Internal ID of the bank connection in our database
    /// </summary>
    public int BankConnectionId { get; init; }

    /// <summary>
    /// ID of the account this connection is linked to
    /// </summary>
    public int AccountId { get; init; }

    /// <summary>
    /// User ID who owns this connection
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Provider identifier (e.g., "akahu", "email-forward")
    /// </summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>
    /// External account identifier from the provider (e.g., "acc_xxx" for Akahu)
    /// </summary>
    public string? ExternalAccountId { get; init; }

    /// <summary>
    /// Provider-specific settings (decrypted OAuth tokens, API keys, etc.)
    /// </summary>
    public Dictionary<string, object> Settings { get; init; } = new();
}

/// <summary>
/// Result of testing a bank connection.
/// Used to validate credentials before saving a connection.
/// </summary>
public record BankConnectionTestResult
{
    /// <summary>
    /// Whether the connection test was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the test failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// External account ID from the provider (populated on success)
    /// </summary>
    public string? ExternalAccountId { get; init; }

    /// <summary>
    /// External account name from the provider (populated on success)
    /// </summary>
    public string? ExternalAccountName { get; init; }

    /// <summary>
    /// Creates a successful test result
    /// </summary>
    public static BankConnectionTestResult Success(string? externalAccountId = null, string? externalAccountName = null)
        => new()
        {
            IsSuccess = true,
            ExternalAccountId = externalAccountId,
            ExternalAccountName = externalAccountName
        };

    /// <summary>
    /// Creates a failed test result
    /// </summary>
    public static BankConnectionTestResult Failure(string errorMessage)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Result of fetching transactions from a bank provider.
/// </summary>
public record BankTransactionFetchResult
{
    /// <summary>
    /// Whether the fetch operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the fetch failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// List of transactions fetched from the provider
    /// </summary>
    public IReadOnlyList<BankTransactionDto> Transactions { get; init; } = Array.Empty<BankTransactionDto>();

    /// <summary>
    /// Total count of transactions returned
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Creates a successful fetch result
    /// </summary>
    public static BankTransactionFetchResult Success(IReadOnlyList<BankTransactionDto> transactions)
        => new()
        {
            IsSuccess = true,
            Transactions = transactions,
            TotalCount = transactions.Count
        };

    /// <summary>
    /// Creates a failed fetch result
    /// </summary>
    public static BankTransactionFetchResult Failure(string errorMessage)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Individual transaction data from a bank provider.
/// </summary>
public record BankTransactionDto
{
    /// <summary>
    /// Unique identifier for this transaction from the provider
    /// </summary>
    public string ExternalId { get; init; } = string.Empty;

    /// <summary>
    /// Transaction date
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Transaction amount (positive for income, negative for expenses)
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Transaction description from the bank
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Transaction reference number (if available)
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>
    /// Category from the provider (if available)
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Merchant name from the provider (if available)
    /// </summary>
    public string? MerchantName { get; init; }

    /// <summary>
    /// Additional metadata from the provider
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Result of fetching account balance from a bank provider.
/// </summary>
public record BankBalanceResult
{
    /// <summary>
    /// Whether the balance fetch was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the fetch failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Current account balance
    /// </summary>
    public decimal CurrentBalance { get; init; }

    /// <summary>
    /// Available balance (may differ from current balance for credit accounts)
    /// </summary>
    public decimal? AvailableBalance { get; init; }

    /// <summary>
    /// Currency code (e.g., "NZD", "USD")
    /// </summary>
    public string Currency { get; init; } = "NZD";

    /// <summary>
    /// Timestamp when this balance was fetched
    /// </summary>
    public DateTime AsOf { get; init; }

    /// <summary>
    /// Creates a successful balance result with current timestamp
    /// </summary>
    public static BankBalanceResult Success(decimal currentBalance, decimal? availableBalance = null, string currency = "NZD")
        => new()
        {
            IsSuccess = true,
            CurrentBalance = currentBalance,
            AvailableBalance = availableBalance,
            Currency = currency,
            AsOf = DateTime.UtcNow
        };

    /// <summary>
    /// Creates a successful balance result with explicit timestamp
    /// </summary>
    public static BankBalanceResult Success(decimal currentBalance, decimal? availableBalance, string currency, DateTime asOf)
        => new()
        {
            IsSuccess = true,
            CurrentBalance = currentBalance,
            AvailableBalance = availableBalance,
            Currency = currency,
            AsOf = asOf
        };

    /// <summary>
    /// Creates a failed balance result
    /// </summary>
    public static BankBalanceResult Failure(string errorMessage)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Information about an available bank provider.
/// Used by the factory to expose available providers to the UI.
/// </summary>
public record BankProviderInfo
{
    /// <summary>
    /// Unique identifier for this provider
    /// </summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>
    /// Display name for UI
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Whether this provider supports real-time webhook notifications
    /// </summary>
    public bool SupportsWebhooks { get; init; }

    /// <summary>
    /// Whether this provider can fetch account balances
    /// </summary>
    public bool SupportsBalanceFetch { get; init; }
}

/// <summary>
/// Result of a bank synchronization operation.
/// </summary>
public record BankSyncResult
{
    /// <summary>
    /// ID of the bank connection that was synced
    /// </summary>
    public int BankConnectionId { get; init; }

    /// <summary>
    /// ID of the sync log entry created for this operation
    /// </summary>
    public int SyncLogId { get; init; }

    /// <summary>
    /// Whether the sync operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the sync failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Total number of transactions processed from the provider
    /// </summary>
    public int TransactionsProcessed { get; init; }

    /// <summary>
    /// Number of new transactions imported
    /// </summary>
    public int TransactionsImported { get; init; }

    /// <summary>
    /// Number of transactions skipped (duplicates)
    /// </summary>
    public int TransactionsSkipped { get; init; }

    /// <summary>
    /// When the sync operation started
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// When the sync operation completed (null if failed before completion)
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// IDs of transactions that were imported during this sync
    /// </summary>
    public List<int> ImportedTransactionIds { get; init; } = new();

    /// <summary>
    /// Creates a successful sync result
    /// </summary>
    public static BankSyncResult Success(
        int bankConnectionId,
        int syncLogId,
        int processed,
        int imported,
        int skipped,
        DateTime startedAt,
        List<int>? importedTransactionIds = null)
        => new()
        {
            BankConnectionId = bankConnectionId,
            SyncLogId = syncLogId,
            IsSuccess = true,
            TransactionsProcessed = processed,
            TransactionsImported = imported,
            TransactionsSkipped = skipped,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            ImportedTransactionIds = importedTransactionIds ?? new()
        };

    /// <summary>
    /// Creates a failed sync result
    /// </summary>
    public static BankSyncResult Failure(
        int bankConnectionId,
        int syncLogId,
        string errorMessage,
        DateTime startedAt)
        => new()
        {
            BankConnectionId = bankConnectionId,
            SyncLogId = syncLogId,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow
        };
}
