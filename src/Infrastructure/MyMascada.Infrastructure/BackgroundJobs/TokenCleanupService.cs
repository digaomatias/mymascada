using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.BackgroundJobs;

public interface ITokenCleanupService
{
    Task CleanupExpiredRefreshTokensAsync();
    Task CleanupExpiredPasswordResetTokensAsync();
}

public class TokenCleanupService : ITokenCleanupService
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly ILogger<TokenCleanupService> _logger;

    public TokenCleanupService(
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        ILogger<TokenCleanupService> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _logger = logger;
    }

    public async Task CleanupExpiredRefreshTokensAsync()
    {
        try
        {
            // Delete tokens that expired/were revoked more than 7 days ago (keep recent ones for audit)
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var deletedCount = await _refreshTokenRepository.DeleteExpiredAndRevokedTokensAsync(cutoff);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Token cleanup: deleted {Count} expired/revoked refresh tokens older than {Cutoff}",
                    deletedCount, cutoff);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired refresh tokens");
        }
    }

    public async Task CleanupExpiredPasswordResetTokensAsync()
    {
        try
        {
            // Delete tokens that expired/were used more than 7 days ago (keep recent ones for audit)
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var deletedCount = await _passwordResetTokenRepository.DeleteExpiredAndUsedTokensAsync(cutoff);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Token cleanup: deleted {Count} expired/used password reset tokens older than {Cutoff}",
                    deletedCount, cutoff);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired password reset tokens");
        }
    }
}
