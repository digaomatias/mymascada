using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Interface for bank data providers (Akahu, email-forward, future integrations).
/// Each bank integration implements this interface to provide a consistent API
/// for fetching transactions and account information.
/// </summary>
public interface IBankProvider
{
    /// <summary>
    /// Unique identifier for this provider (e.g., "akahu", "email-forward")
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Display name for UI (e.g., "Akahu (NZ Banks)")
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this provider supports real-time webhook notifications
    /// </summary>
    bool SupportsWebhooks { get; }

    /// <summary>
    /// Whether this provider can fetch account balances
    /// </summary>
    bool SupportsBalanceFetch { get; }

    /// <summary>
    /// Test the connection with the given configuration.
    /// Validates that the provider credentials are valid and the connection can be established.
    /// </summary>
    /// <param name="config">The bank connection configuration containing credentials and settings</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure with error details</returns>
    Task<BankConnectionTestResult> TestConnectionAsync(BankConnectionConfig config, CancellationToken ct = default);

    /// <summary>
    /// Fetch transactions for the given date range from the bank provider.
    /// </summary>
    /// <param name="config">The bank connection configuration containing credentials and settings</param>
    /// <param name="from">Start date for the transaction fetch (inclusive)</param>
    /// <param name="to">End date for the transaction fetch (inclusive)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing fetched transactions or error details</returns>
    Task<BankTransactionFetchResult> FetchTransactionsAsync(BankConnectionConfig config, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Fetch current account balance from the bank provider (if supported).
    /// Returns null if balance fetching is not supported by this provider.
    /// </summary>
    /// <param name="config">The bank connection configuration containing credentials and settings</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing balance information, null if not supported, or error details</returns>
    Task<BankBalanceResult?> FetchBalanceAsync(BankConnectionConfig config, CancellationToken ct = default);

    /// <summary>
    /// Fetch pending transactions summary for the account.
    /// Pending transactions are included in the bank's current balance but are not yet
    /// available as cleared transactions. Used to adjust balance comparison during reconciliation.
    /// </summary>
    /// <param name="config">The bank connection configuration containing credentials and settings</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Summary of pending transactions (total amount and count). Returns zeros if not supported.</returns>
    Task<PendingTransactionsSummary> FetchPendingTransactionsSummaryAsync(BankConnectionConfig config, CancellationToken ct = default)
        => Task.FromResult(new PendingTransactionsSummary(0m, 0));
}

/// <summary>
/// Summary of pending transactions for balance adjustment.
/// </summary>
public record PendingTransactionsSummary(decimal Total, int Count);
