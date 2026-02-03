namespace MyMascada.Infrastructure.Services.BankIntegration.Providers;

/// <summary>
/// Settings stored encrypted for an Akahu bank connection.
/// These are per-connection sync state stored in BankConnection.EncryptedSettings.
/// NOTE: Access tokens are now stored per-user in AkahuUserCredential, not per-connection.
/// </summary>
public class AkahuConnectionSettings
{
    /// <summary>
    /// Akahu account ID (acc_xxx) that this connection is linked to.
    /// </summary>
    public string AkahuAccountId { get; set; } = string.Empty;

    /// <summary>
    /// Last transaction ID that was successfully synced (for incremental sync).
    /// </summary>
    public string? LastSyncedTransactionId { get; set; }

    /// <summary>
    /// Timestamp of the last successful sync.
    /// </summary>
    public DateTime? LastSyncTimestamp { get; set; }
}

/// <summary>
/// Akahu application configuration (from appsettings, not encrypted per-connection).
/// NOTE: For Personal App mode, tokens are stored per-user in AkahuUserCredential,
/// NOT in this configuration. These options are primarily for Production App OAuth mode.
/// </summary>
public class AkahuOptions
{
    public const string SectionName = "Akahu";

    /// <summary>
    /// Whether the Akahu bank sync feature is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Akahu App ID Token for Production Apps (app_token_xxx).
    /// For Personal Apps, this is stored per-user in AkahuUserCredential instead.
    /// </summary>
    public string AppIdToken { get; set; } = string.Empty;

    /// <summary>
    /// Akahu App Secret (only needed for Production Apps with OAuth flow).
    /// </summary>
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>
    /// OAuth redirect URI (for Production Apps).
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Default OAuth scopes.
    /// </summary>
    public string[] DefaultScopes { get; set; } = new[] { "ENDURING_CONSENT" };

    /// <summary>
    /// Akahu API base URL (must end with trailing slash for proper URL resolution).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.akahu.io/v1/";

    /// <summary>
    /// Akahu OAuth base URL.
    /// </summary>
    public string OAuthBaseUrl { get; set; } = "https://oauth.akahu.nz";
}
