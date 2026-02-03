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
    /// <param name="accessToken">User's access token to revoke</param>
    /// <param name="ct">Cancellation token</param>
    Task RevokeTokenAsync(string accessToken, CancellationToken ct = default);
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
}
