using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public TokenService(
        IConfiguration configuration,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _configuration = configuration;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public Task<string> GenerateJwtTokenAsync(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured"));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim("Currency", user.Currency),
            new Claim("TimeZone", user.TimeZone)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"]
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Task.FromResult(tokenHandler.WriteToken(token));
    }

    public async Task<RefreshTokenResult> GenerateRefreshTokenAsync(User user, string ipAddress)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var rawToken = Convert.ToBase64String(randomBytes);
        var tokenHash = HashToken(rawToken);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = tokenHash,
            ExpiryDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(30), DateTimeKind.Utc),
            UserId = user.Id,
            CreatedByIp = ipAddress,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        return new RefreshTokenResult(rawToken, refreshToken.ExpiryDate);
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(tokenHash);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            return null;
        }

        return refreshToken;
    }

    public async Task<AuthenticationResponse> RefreshTokenAsync(string rawToken, string ipAddress)
    {
        try
        {
            var token = await ValidateRefreshTokenAsync(rawToken);

            if (token == null)
            {
                return new AuthenticationResponse
                {
                    IsSuccess = false,
                    Errors = new List<string> { "Invalid or expired refresh token" }
                };
            }

            var user = token.User;

            // Generate new tokens
            var newJwtToken = await GenerateJwtTokenAsync(user);
            var newRefreshTokenResult = await GenerateRefreshTokenAsync(user, ipAddress);

            // Revoke the old refresh token, recording the hash of the replacement
            var newTokenHash = HashToken(newRefreshTokenResult.RawToken);
            token.Revoke(ipAddress, newTokenHash);
            await _refreshTokenRepository.UpdateAsync(token);

            return new AuthenticationResponse
            {
                IsSuccess = true,
                Token = newJwtToken,
                RefreshToken = newRefreshTokenResult.RawToken,
                RefreshTokenExpiresAt = newRefreshTokenResult.ExpiresAt,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    UserName = user.UserName,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = $"{user.FirstName} {user.LastName}".Trim(),
                    Currency = user.Currency,
                    TimeZone = user.TimeZone,
                    AiDescriptionCleaning = user.AiDescriptionCleaning
                }
            };
        }
        catch (Exception ex)
        {
            return new AuthenticationResponse
            {
                IsSuccess = false,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task RevokeRefreshTokenAsync(string rawToken, string ipAddress)
    {
        var tokenHash = HashToken(rawToken);
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(tokenHash);

        if (refreshToken != null && refreshToken.IsActive)
        {
            refreshToken.Revoke(ipAddress);
            await _refreshTokenRepository.UpdateAsync(refreshToken);
        }
    }

    public async Task RevokeAllUserRefreshTokensAsync(Guid userId, string ipAddress)
    {
        await _refreshTokenRepository.RevokeAllUserTokensAsync(userId, ipAddress);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(bytes);
    }
}
