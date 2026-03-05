using MyMascada.Domain.Entities;
using MyMascada.Application.Features.Authentication.DTOs;

namespace MyMascada.Application.Common.Interfaces;

public interface IAuthenticationService
{
    Task<string> HashPasswordAsync(string password);
    Task<bool> VerifyPasswordAsync(string password, string hashedPassword);
    bool NeedsRehash(string hashedPassword);
    string GenerateSecurityStamp();
    Task<AuthenticationResponse> GoogleLoginAsync(string email, string? firstName, string? lastName, string? googleId, string? inviteCode = null);
    Task<AuthenticationResponse> GoogleTokenLoginAsync(string idToken);
    Task<AuthenticationResponse> RefreshTokenAsync(string rawToken, string ipAddress);
    Task RevokeRefreshTokenAsync(string rawToken, string ipAddress);
}
