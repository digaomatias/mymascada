using MyMascada.Domain.Entities;
using MyMascada.Application.Features.Authentication.DTOs;

namespace MyMascada.Application.Common.Interfaces;

public interface IAuthenticationService
{
    Task<string> GenerateJwtTokenAsync(User user);
    Task<string> HashPasswordAsync(string password);
    Task<bool> VerifyPasswordAsync(string password, string hashedPassword);
    string GenerateSecurityStamp();
    Task<AuthenticationResponse> GoogleLoginAsync(string email, string? firstName, string? lastName, string? googleId);
    Task<AuthenticationResponse> GoogleTokenLoginAsync(string idToken);
    
    // Refresh token methods
    Task<RefreshToken> GenerateRefreshTokenAsync(User user, string ipAddress);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string token);
    Task<AuthenticationResponse> RefreshTokenAsync(string refreshToken, string ipAddress);
    Task RevokeRefreshTokenAsync(string token, string ipAddress, string? replacedByToken = null);
    Task RevokeAllUserRefreshTokensAsync(Guid userId, string ipAddress);
}