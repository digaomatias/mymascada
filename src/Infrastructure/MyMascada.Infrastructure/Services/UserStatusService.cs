using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services;

public class UserStatusService : IUserStatusService
{
    private readonly IUserFinancialProfileRepository _financialProfileRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUserAiSettingsRepository _aiSettingsRepository;
    private readonly IFeatureFlags _featureFlags;

    public UserStatusService(
        IUserFinancialProfileRepository financialProfileRepository,
        IAccountRepository accountRepository,
        IUserAiSettingsRepository aiSettingsRepository,
        IFeatureFlags featureFlags)
    {
        _financialProfileRepository = financialProfileRepository;
        _accountRepository = accountRepository;
        _aiSettingsRepository = aiSettingsRepository;
        _featureFlags = featureFlags;
    }

    public async Task<(bool IsOnboardingComplete, bool HasAiConfigured)> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        // Sequential — all repositories share a scoped DbContext which is not thread-safe
        var financialProfile = await _financialProfileRepository.GetByUserIdAsync(userId, ct);
        var accounts = await _accountRepository.GetByUserIdAsync(userId);
        var aiSettings = await _aiSettingsRepository.GetByUserIdAsync(userId);

        var hasAccounts = accounts.Any();
        var isOnboardingComplete = (financialProfile != null && financialProfile.OnboardingCompleted) || hasAccounts;
        var hasAiConfigured = (aiSettings != null && !string.IsNullOrEmpty(aiSettings.EncryptedApiKey))
            || _featureFlags.HasGlobalAiKey;

        return (isOnboardingComplete, hasAiConfigured);
    }
}
