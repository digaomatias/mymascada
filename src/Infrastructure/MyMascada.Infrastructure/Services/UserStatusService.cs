using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services;

public class UserStatusService : IUserStatusService
{
    private readonly IUserFinancialProfileRepository _financialProfileRepository;
    private readonly IUserAiSettingsRepository _aiSettingsRepository;
    private readonly IFeatureFlags _featureFlags;

    public UserStatusService(
        IUserFinancialProfileRepository financialProfileRepository,
        IUserAiSettingsRepository aiSettingsRepository,
        IFeatureFlags featureFlags)
    {
        _financialProfileRepository = financialProfileRepository;
        _aiSettingsRepository = aiSettingsRepository;
        _featureFlags = featureFlags;
    }

    public async Task<(bool IsOnboardingComplete, bool HasAiConfigured)> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        // Sequential — repositories share a scoped DbContext which is not thread-safe
        var financialProfile = await _financialProfileRepository.GetByUserIdAsync(userId, ct);
        var aiSettings = await _aiSettingsRepository.GetByUserIdAsync(userId);

        // Align with canonical source: GetOnboardingStatusQuery checks only OnboardingCompleted
        var isOnboardingComplete = financialProfile?.OnboardingCompleted ?? false;
        var hasAiConfigured = (aiSettings != null && !string.IsNullOrEmpty(aiSettings.EncryptedApiKey))
            || _featureFlags.HasGlobalAiKey;

        return (isOnboardingComplete, hasAiConfigured);
    }
}
