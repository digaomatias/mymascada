namespace MyMascada.Domain.Enums;

/// <summary>
/// Subscription tier that determines access to AI-powered features.
/// </summary>
public enum SubscriptionTier
{
    /// <summary>
    /// Free tier: Rules + BankCategory + ML only. No LLM or AI rule suggestions.
    /// </summary>
    Free = 0,

    /// <summary>
    /// Pro/Premium tier: AI rule suggestions (5/month) + limited LLM categorization (50 direct, 200 bulk per month).
    /// </summary>
    Pro = 1,

    /// <summary>
    /// Family tier: same as Pro but for household accounts.
    /// </summary>
    Family = 2,

    /// <summary>
    /// Self-hosted: unlimited access, user provides their own API key (BYOK).
    /// Automatically assigned when IFeatureFlags.StripeBilling is false.
    /// </summary>
    SelfHosted = 3
}
