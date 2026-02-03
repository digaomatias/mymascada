using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<IEnumerable<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId);
    Task AddAsync(RefreshToken refreshToken);
    Task UpdateAsync(RefreshToken refreshToken);
    Task DeleteAsync(RefreshToken refreshToken);
    Task RevokeAllUserTokensAsync(Guid userId, string ipAddress);
    Task<int> DeleteExpiredAndRevokedTokensAsync(DateTime olderThan);
}