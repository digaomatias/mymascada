namespace MyMascada.Domain.Features;

/// <summary>
/// Catalogue of user-facing feature toggles and their launch defaults.
///
/// These are distinct from the global <c>IFeatureFlags</c> (which describe what
/// the server is *configured* to do). A feature is shown to a user only when it
/// is enabled for them AND the corresponding global capability is available.
/// </summary>
public static class UserFeatureKeys
{
    /// <summary>AI assistant / chat ("Ask Alce"). Expensive — off until billing is defined.</summary>
    public const string AiAssistant = "ai_assistant";

    /// <summary>On-device camera receipt scanning.</summary>
    public const string ReceiptScan = "receipt_scan";

    /// <summary>Bank connections / Akahu sync.</summary>
    public const string BankConnections = "bank_connections";

    /// <summary>
    /// Every known feature with its launch default. All gated features default to
    /// OFF for v1 so nothing expensive or unfinished is exposed until explicitly
    /// enabled (globally via config, or per-user via the admin module).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, bool> LaunchDefaults =
        new Dictionary<string, bool>
        {
            [AiAssistant] = false,
            [ReceiptScan] = false,
            [BankConnections] = false,
        };

    public static readonly IReadOnlyCollection<string> All = LaunchDefaults.Keys.ToArray();
}
