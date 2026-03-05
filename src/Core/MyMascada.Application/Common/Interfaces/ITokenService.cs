using MyMascada.Domain.Entities;
using MyMascada.Application.Features.Authentication.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Handles JWT and refresh token generation, validation, and revocation.
/// Refresh tokens are stored as SHA256 hashes for security.
/// </summary>
public interface ITokenService
{
    Task<string> GenerateJwtTokenAsync(User user);
    Task<RefreshTokenResult> GenerateRefreshTokenAsync(User user, string ipAddress);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string rawToken);
    Task<AuthenticationResponse> RefreshTokenAsync(string rawToken, string ipAddress);
    Task RevokeRefreshTokenAsync(string rawToken, string ipAddress);
    Task RevokeAllUserRefreshTokensAsync(Guid userId, string ipAddress);
}

/// <summary>
/// Result of generating a refresh token. Contains the raw token (to send to the client)
/// and the expiry date. The hashed token is stored in the database.
/// </summary>
public record RefreshTokenResult(string RawToken, DateTime ExpiresAt);
