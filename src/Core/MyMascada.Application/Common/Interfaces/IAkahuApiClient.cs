namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Interface for Akahu API operations.
/// This abstraction allows the Application layer to interact with Akahu
/// without depending on Infrastructure layer directly.
/// </summary>
public interface IAkahuApiClient
{
    /// <summary>
    /// Gets the OAuth authorization URL to redirect the user to.
    /// For Production App OAuth mode only.
    /// </summary>
    /// <param name="state">CSRF protection state parameter</param>
    /// <param name="email">Optional email to pre-fill in the Akahu login form</param>
    /// <returns>The authorization URL</returns>
    string GetAuthorizationUrl(string state, string? email = null);

    /// <summary>
    /// Exchanges an OAuth authorization code for an access token.
    /// For Production App OAuth mode only.
    /// </summary>
    /// <param name="code">The authorization code from the OAuth callback</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Token response containing access token</returns>
    Task<AkahuTokenResponse> ExchangeCodeForTokenAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Gets all accounts for the user using their credentials.
    /// For Personal App mode: Both tokens are provided by the user.
    /// For OAuth mode: appIdToken from config, userToken from OAuth.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token (app_token_xxx)</param>
    /// <param name="userToken">User's access token (user_token_xxx)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of Akahu accounts</returns>
    Task<IReadOnlyList<AkahuAccountInfo>> GetAccountsWithCredentialsAsync(
        string appIdToken,
        string userToken,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a specific account by ID using user credentials.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token (app_token_xxx)</param>
    /// <param name="userToken">User's access token (user_token_xxx)</param>
    /// <param name="accountId">Akahu account ID (acc_xxx)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Account info, or null if not found</returns>
    Task<AkahuAccountInfo?> GetAccountWithCredentialsAsync(
        string appIdToken,
        string userToken,
        string accountId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists the user's bank connections (conn_xxx). Used during the classic-to-official
    /// migration window to detect which connections have a `_classic` predecessor.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token (app_token_xxx)</param>
    /// <param name="userToken">User's access token (user_token_xxx)</param>
    /// <param name="ct">Cancellation token</param>
    Task<IReadOnlyList<AkahuConnectionInfo>> GetConnectionsWithCredentialsAsync(
        string appIdToken,
        string userToken,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches transactions for a given Akahu account between two dates. Returned DTOs include
    /// the `Migrated` field so callers can build classic-to-official transaction ID maps during
    /// the Akahu open-banking migration.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token (app_token_xxx)</param>
    /// <param name="userToken">User's access token (user_token_xxx)</param>
    /// <param name="accountId">Akahu account ID (acc_xxx)</param>
    /// <param name="start">Inclusive start date (null for no lower bound)</param>
    /// <param name="end">Inclusive end date (null for no upper bound)</param>
    /// <param name="ct">Cancellation token</param>
    Task<IReadOnlyList<MyMascada.Application.Features.BankConnections.DTOs.BankTransactionDto>> GetTransactionsWithCredentialsAsync(
        string appIdToken,
        string userToken,
        string accountId,
        DateTime? start = null,
        DateTime? end = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validates that the provided credentials are valid by making a test API call.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token (app_token_xxx)</param>
    /// <param name="userToken">User's access token (user_token_xxx)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if credentials are valid</returns>
    Task<bool> ValidateCredentialsAsync(
        string appIdToken,
        string userToken,
        CancellationToken ct = default);

    /// <summary>
    /// Revokes the user's access token.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token used to authenticate the revocation request</param>
    /// <param name="accessToken">User's access token to revoke</param>
    /// <param name="ct">Cancellation token</param>
    Task RevokeTokenAsync(string appIdToken, string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to an Akahu webhook type for the given user.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token</param>
    /// <param name="userToken">User's access token</param>
    /// <param name="webhookType">Webhook type (TOKEN, ACCOUNT, TRANSACTION)</param>
    /// <param name="state">State value passed back in webhook events (e.g. user ID)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created webhook subscription as returned by Akahu.</returns>
    Task<AkahuWebhookSubscriptionInfo> SubscribeToWebhookAsync(string appIdToken, string userToken, string webhookType, string? state = null, CancellationToken ct = default);

    /// <summary>
    /// Unsubscribes from an Akahu webhook.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token</param>
    /// <param name="userToken">User's access token</param>
    /// <param name="webhookId">The webhook subscription ID to remove</param>
    /// <param name="ct">Cancellation token</param>
    Task UnsubscribeFromWebhookAsync(string appIdToken, string userToken, string webhookId, CancellationToken ct = default);

    /// <summary>
    /// Lists all webhook subscriptions for the user.
    /// </summary>
    /// <param name="appIdToken">Akahu App ID Token</param>
    /// <param name="userToken">User's access token</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of webhook subscriptions</returns>
    Task<IReadOnlyList<AkahuWebhookSubscriptionInfo>> ListWebhooksAsync(string appIdToken, string userToken, CancellationToken ct = default);
}

/// <summary>
/// Represents an Akahu webhook subscription returned by the API.
/// </summary>
public record AkahuWebhookSubscriptionInfo
{
    public string Id { get; init; } = string.Empty;
    public string WebhookType { get; init; } = string.Empty;
    public string? State { get; init; }
}

/// <summary>
/// Token response from Akahu OAuth flow.
/// </summary>
public record AkahuTokenResponse
{
    /// <summary>
    /// Access token for API calls
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Token type (typically "Bearer")
    /// </summary>
    public string TokenType { get; init; } = string.Empty;

    /// <summary>
    /// OAuth scopes granted
    /// </summary>
    public string? Scope { get; init; }
}

/// <summary>
/// Akahu account information.
/// </summary>
public record AkahuAccountInfo
{
    /// <summary>
    /// Akahu account ID (acc_xxx)
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
    /// Account status (ACTIVE, INACTIVE)
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Current balance (if available)
    /// </summary>
    public decimal? CurrentBalance { get; init; }

    /// <summary>
    /// Available balance (if available)
    /// </summary>
    public decimal? AvailableBalance { get; init; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; init; } = "NZD";

    /// <summary>
    /// Name of the bank (e.g., "ANZ")
    /// </summary>
    public string BankName { get; init; } = string.Empty;

    /// <summary>
    /// The Akahu ID of the classic account this account was migrated from (the official open
    /// banking migration carries this on the new account record). Null when no migration applies.
    /// </summary>
    public string? Migrated { get; init; }

    /// <summary>
    /// The Akahu ID of the classic connection this account's connection was migrated from.
    /// Sourced from the `_classic` field on the parent connection. Null for native official
    /// connections (no classic predecessor) and for classic connections themselves.
    /// </summary>
    public string? ConnectionClassic { get; init; }
}

/// <summary>
/// Represents an Akahu bank connection (conn_xxx) — the linkage between the user and a
/// specific bank in Akahu. During the classic-to-official migration window, an official
/// connection may carry a `_classic` reference back to the predecessor classic connection.
/// </summary>
public record AkahuConnectionInfo
{
    /// <summary>
    /// Akahu connection ID (conn_xxx).
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the bank (e.g., "ANZ").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Optional URL of the bank's logo.
    /// </summary>
    public string? Logo { get; init; }

    /// <summary>
    /// When this is an official open-banking connection that was migrated from a classic
    /// predecessor, this is the classic connection's ID. Null otherwise.
    /// </summary>
    public string? Classic { get; init; }
}
