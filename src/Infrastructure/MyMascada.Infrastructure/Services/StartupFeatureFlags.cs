using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services;

/// <summary>
/// Feature flags evaluated once at application startup from IConfiguration.
/// Registered as singleton so the values are computed only once.
/// </summary>
public class StartupFeatureFlags : IFeatureFlags
{
    public bool AiCategorization { get; }
    public bool EmailNotifications { get; }
    public bool GoogleOAuth { get; }
    public bool BankSync { get; }
    public bool AccountSharing { get; }

    public StartupFeatureFlags(
        bool aiCategorization,
        bool emailNotifications,
        bool googleOAuth,
        bool bankSync,
        bool accountSharing = false)
    {
        AiCategorization = aiCategorization;
        EmailNotifications = emailNotifications;
        GoogleOAuth = googleOAuth;
        BankSync = bankSync;
        AccountSharing = accountSharing;
    }
}
