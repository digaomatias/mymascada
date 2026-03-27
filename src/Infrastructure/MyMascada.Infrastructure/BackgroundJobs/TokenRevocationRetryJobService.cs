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

                    var attemptNumber = credential.RevocationFailureCount + 1;
                    await akahuApiClient.RevokeTokenAsync(appIdToken, accessToken);

                    // Token revoked successfully — persist the cleared state
                    credential.IsRevocationPending = false;
                    credential.RevocationFailedAt = null;
                    credential.RevocationFailureCount = 0;

                    try
                    {
                        await credentialRepository.UpdateAsync(credential);
                    }
                    catch (Exception persistEx)
                    {
                        // Token is already revoked at Akahu — log but don't fall into failure handler.
                        // Next run will re-attempt revocation (which is a no-op) and try to clear the flag again.
                        _logger.LogWarning(persistEx,
                            "Token for user {UserId} was revoked successfully but failed to persist cleared state. " +
                            "Will be retried on next run.",
                            credential.UserId);
                        continue;
                    }

                    successCount++;
                    _logger.LogInformation(
                        "Successfully revoked token for user {UserId} on retry attempt {Attempt}",
                        credential.UserId, attemptNumber);
                }
                catch (Exception ex)
                {
                    failCount++;
                    credential.RevocationFailureCount++;
                    credential.RevocationFailedAt = DateTime.UtcNow;

                    _logger.LogError(ex,
                        "Retry failed for user {UserId} (attempt {Attempt}/{MaxAttempts})",
                        credential.UserId, credential.RevocationFailureCount, MaxRetryAttempts);

                    try
                    {
                        // If max retries reached, abandon to prevent further retries
                        if (credential.RevocationFailureCount >= MaxRetryAttempts)
                        {
                            credential.IsRevocationPending = false;
                            _logger.LogError(
                                "Max retry attempts reached for user {UserId}. Marking as abandoned. Manual intervention required.",
                                credential.UserId);
                        }

                        await credentialRepository.UpdateAsync(credential);
                    }
                    catch (Exception persistEx)
                    {
                        // Cannot persist the failure count. Mark as abandoned to break the retry cycle.
                        _logger.LogCritical(persistEx,
                            "Failed to persist failure count for user {UserId} (attempt {Attempt}). " +
                            "Credential may be retried with stale count on next run.",
                            credential.UserId, credential.RevocationFailureCount);

                        try
                        {
                            credential.IsRevocationPending = false;
                            await credentialRepository.UpdateAsync(credential);
                            _logger.LogWarning(
                                "Abandoned revocation retry for user {UserId} after persist failure to prevent infinite loop.",
                                credential.UserId);
                            abandonedCount++;
                        }
                        catch
                        {
                            // DB is completely unavailable for this credential — nothing more we can do.
                            // The Hangfire job-level retry (AutomaticRetry) will handle rescheduling.
                            _logger.LogCritical(
                                "Cannot persist any state for user {UserId}. Credential will be retried on next job run.",
                                credential.UserId);
                        }
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
