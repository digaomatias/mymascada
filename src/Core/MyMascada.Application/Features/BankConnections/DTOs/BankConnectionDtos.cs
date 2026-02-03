namespace MyMascada.Application.Features.BankConnections.DTOs;

/// <summary>
/// DTO for bank connection list display.
/// </summary>
public record BankConnectionDto
{
    /// <summary>
    /// Unique identifier of the bank connection
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// ID of the linked MyMascada account
    /// </summary>
    public int AccountId { get; init; }

    /// <summary>
    /// Name of the linked MyMascada account
    /// </summary>
    public string AccountName { get; init; } = string.Empty;

    /// <summary>
    /// Bank provider identifier (e.g., "akahu")
    /// </summary>
    public string ProviderId { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the bank provider
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// External account identifier from the provider (e.g., "acc_xxx")
    /// </summary>
    public string? ExternalAccountId { get; init; }

    /// <summary>
    /// Display name of the account from the provider
    /// </summary>
    public string? ExternalAccountName { get; init; }

    /// <summary>
    /// Whether this connection is currently active
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// UTC timestamp of the last successful sync
    /// </summary>
    public DateTime? LastSyncAt { get; init; }

    /// <summary>
    /// Error message from the last failed sync attempt
    /// </summary>
    public string? LastSyncError { get; init; }

    /// <summary>
    /// When the connection was created
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// DTO for detailed bank connection view, including recent sync logs.
/// </summary>
public record BankConnectionDetailDto : BankConnectionDto
{
    /// <summary>
    /// Recent synchronization logs for this connection
    /// </summary>
    public IEnumerable<BankSyncLogDto> RecentSyncLogs { get; init; } = Enumerable.Empty<BankSyncLogDto>();
}

/// <summary>
/// DTO for bank sync log entries.
/// </summary>
public record BankSyncLogDto
{
    /// <summary>
    /// Unique identifier of the sync log
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Type of sync operation (Manual, Scheduled, Webhook, Initial)
    /// </summary>
    public string SyncType { get; init; } = string.Empty;

    /// <summary>
    /// Status of the sync operation (InProgress, Completed, Failed, PartialSuccess)
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the sync started
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// UTC timestamp when the sync completed (null if still in progress)
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Total transactions processed from the provider
    /// </summary>
    public int TransactionsProcessed { get; init; }

    /// <summary>
    /// New transactions imported into the system
    /// </summary>
    public int TransactionsImported { get; init; }

    /// <summary>
    /// Transactions skipped (duplicates)
    /// </summary>
    public int TransactionsSkipped { get; init; }

    /// <summary>
    /// Error message if the sync failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DTO for sync operation result returned by sync commands.
/// </summary>
public record BankSyncResultDto
{
    /// <summary>
    /// ID of the bank connection that was synced
    /// </summary>
    public int BankConnectionId { get; init; }

    /// <summary>
    /// Whether the sync was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if the sync failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of transactions imported
    /// </summary>
    public int TransactionsImported { get; init; }

    /// <summary>
    /// Number of transactions skipped (duplicates)
    /// </summary>
    public int TransactionsSkipped { get; init; }
}

/// <summary>
/// DTO for Akahu account information returned during OAuth flow.
/// </summary>
public record AkahuAccountDto
{
    /// <summary>
    /// Akahu account ID (e.g., "acc_xxx")
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the account
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Formatted account number (e.g., "00-0000-0000000-00")
    /// </summary>
    public string FormattedAccount { get; init; } = string.Empty;

    /// <summary>
    /// Account type (CHECKING, SAVINGS, CREDITCARD, etc.)
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Name of the bank (e.g., "ANZ")
    /// </summary>
    public string BankName { get; init; } = string.Empty;

    /// <summary>
    /// Current account balance (if available)
    /// </summary>
    public decimal? CurrentBalance { get; init; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; init; } = "NZD";

    /// <summary>
    /// Whether this Akahu account is already linked to a MyMascada account
    /// </summary>
    public bool IsAlreadyLinked { get; init; }
}

/// <summary>
/// Result of initiating the Akahu connection flow.
/// </summary>
public record InitiateConnectionResult
{
    /// <summary>
    /// URL to redirect the user to for OAuth authorization.
    /// Only set for Production Apps (when IsPersonalAppMode = false).
    /// </summary>
    public string? AuthorizationUrl { get; init; }

    /// <summary>
    /// State parameter to validate callback (for CSRF protection).
    /// Only set for Production Apps.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Whether the app is running in Personal App mode.
    /// In Personal App mode, accounts are fetched directly using user's stored credentials.
    /// </summary>
    public bool IsPersonalAppMode { get; init; }

    /// <summary>
    /// Whether the user needs to set up their Akahu credentials first.
    /// If true, the frontend should show the credential setup dialog.
    /// </summary>
    public bool RequiresCredentials { get; init; }

    /// <summary>
    /// Error message if there was a problem with the stored credentials.
    /// Only set when RequiresCredentials = true due to credential issues.
    /// </summary>
    public string? CredentialsError { get; init; }

    /// <summary>
    /// Available Akahu accounts (only populated when credentials are valid).
    /// </summary>
    public IEnumerable<AkahuAccountDto> AvailableAccounts { get; init; } = Enumerable.Empty<AkahuAccountDto>();
}
