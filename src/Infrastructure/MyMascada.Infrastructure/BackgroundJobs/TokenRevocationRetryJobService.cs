using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-based background job that retries failed Akahu token revocations.
/// Runs daily to pick up credentials flagged as having pending revocations.
/// Gives up after 5 consecutive failures to avoid infinite retries.
/// </summary>
public class TokenRevocationRetryJobService : ITokenRevocationRetryJobService
{
    private const int MaxRetryAttempts = 5;

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<TokenRevocationRetryJobService> _logger;

    public TokenRevocationRetryJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TokenRevocationRetryJobService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Retries all pending token revocations.
    /// Scheduled to run daily at 3:30 AM.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task RetryPendingRevocationsAsync()
    {
        _logger.LogInformation("Starting token revocation retry job");

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var credentialRepository = scope.ServiceProvider.GetRequiredService<IAkahuUserCredentialRepository>();
            var akahuApiClient = scope.ServiceProvider.GetRequiredService<IAkahuApiClient>();
            var encryptionService = scope.ServiceProvider.GetRequiredService<ISettingsEncryptionService>();

            var pendingCredentials = await credentialRepository.GetPendingRevocationsAsync();

            if (pendingCredentials.Count == 0)
            {
                _logger.LogDebug("No pending token revocations to retry");
                return;
            }

            _logger.LogInformation("Found {Count} credentials with pending token revocations", pendingCredentials.Count);

            var successCount = 0;
            var failCount = 0;
            var abandonedCount = 0;

            foreach (var credential in pendingCredentials)
            {
                // Circuit breaker: treat corrupted retry counts as max retries exceeded
                if (credential.RevocationFailureCount < 0 || credential.RevocationFailureCount >= MaxRetryAttempts)
                {
                    _logger.LogError(
                        "Abandoning token revocation for user {UserId} after {Attempts} failed attempts. Manual intervention required.",
                        credential.UserId, credential.RevocationFailureCount);
                    abandonedCount++;
                    continue;
                }

                try
                {
                    var appIdToken = encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
                    var accessToken = encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);

                    if (string.IsNullOrEmpty(appIdToken) || string.IsNullOrEmpty(accessToken))
                    {
                        _logger.LogError(
                            "Cannot retry revocation for user {UserId}: tokens are empty. Clearing pending flag.",
                            credential.UserId);
                        credential.IsRevocationPending = false;
                        await credentialRepository.UpdateAsync(credential);
                        continue;
                    }

                    await akahuApiClient.RevokeTokenAsync(appIdToken, accessToken);

                    // Success — clear the pending flag
                    credential.IsRevocationPending = false;
                    credential.RevocationFailedAt = null;
                    credential.RevocationFailureCount = 0;
                    await credentialRepository.UpdateAsync(credential);
                    successCount++;

                    _logger.LogInformation(
                        "Successfully revoked token for user {UserId} on retry attempt {Attempt}",
                        credential.UserId, credential.RevocationFailureCount + 1);
                }
                catch (Exception ex)
                {
                    failCount++;

                    _logger.LogError(ex,
                        "Retry failed for user {UserId} (attempt {Attempt}/{MaxAttempts})",
                        credential.UserId, credential.RevocationFailureCount + 1, MaxRetryAttempts);

                    try
                    {
                        credential.RevocationFailureCount++;
                        credential.RevocationFailedAt = DateTime.UtcNow;
                        await credentialRepository.UpdateAsync(credential);
                    }
                    catch (Exception persistEx)
                    {
                        _logger.LogError(persistEx,
                            "Failed to persist retry count for user {UserId}. Skipping to avoid infinite retry loop.",
                            credential.UserId);
                    }
                }
            }

            _logger.LogInformation(
                "Token revocation retry job completed: {Success} succeeded, {Failed} failed, {Abandoned} abandoned",
                successCount, failCount, abandonedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token revocation retry job failed unexpectedly");
            throw; // Let Hangfire retry
        }
    }
}
