namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Configuration options for the Stripe billing integration.
/// </summary>
public class StripeOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Stripe";

    /// <summary>
    /// Whether Stripe billing is enabled. When false, the application operates in free-tier-only mode.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Stripe secret API key (sk_live_... or sk_test_...)
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe publishable API key (pk_live_... or pk_test_...)
    /// </summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Stripe webhook signing secret (whsec_...)
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
