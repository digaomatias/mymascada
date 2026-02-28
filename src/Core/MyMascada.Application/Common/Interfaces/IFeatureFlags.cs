namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Provides feature availability flags computed once at startup from configuration.
/// Injected via DI so consumers never need to inspect raw configuration.
/// </summary>
public interface IFeatureFlags
{
    bool AiCategorization { get; }
    bool EmailNotifications { get; }
    bool GoogleOAuth { get; }
    bool BankSync { get; }
    bool HasGlobalAiKey { get; }
    bool StripeBilling { get; }
}
