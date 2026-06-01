namespace MyMascada.Infrastructure.Services.BankIntegration.Providers;

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

    /// <summary>
    /// How long to cache webhook signing keys, in minutes.
    /// Default: 1440 (24 hours).
    /// </summary>
    public int WebhookSigningKeysCacheMinutes { get; set; } = 1440;

    /// <summary>
    /// Whether to loosen duplicate-detection tolerance for Akahu connections that were
    /// migrated to official open banking within the last 30 days. Default: true.
    /// </summary>
    public bool MigrationFallbackEnabled { get; set; } = true;
}
