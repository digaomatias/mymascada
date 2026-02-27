using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Domain.Entities;
using Google.Apis.Auth;

namespace MyMascada.Infrastructure.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ICategorySeedingService _categorySeedingService;
    private readonly IInviteCodeValidationService _inviteCodeValidationService;
    private readonly BetaAccessOptions _betaAccessOptions;

    public AuthenticationService(
        IConfiguration configuration,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ICategorySeedingService categorySeedingService,
        IInviteCodeValidationService inviteCodeValidationService,
        IOptions<BetaAccessOptions> betaAccessOptions)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _categorySeedingService = categorySeedingService;
        _inviteCodeValidationService = inviteCodeValidationService;
        _betaAccessOptions = betaAccessOptions.Value;
    }

    public async Task<string> GenerateJwtTokenAsync(User user)
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
        return await Task.FromResult(tokenHandler.WriteToken(token));
    }

    private const int CurrentIterations = 600000;
    private const int LegacyIterations = 10000;
    private const string CurrentHashPrefix = "v2$";

    public async Task<string> HashPasswordAsync(string password)
    {
        // Generate a salt
        var salt = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        // Hash the password with the salt using PBKDF2 at the current iteration count
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, CurrentIterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);

        // Combine salt and hash; prefix with version marker
        var hashBytes = new byte[48];
        Array.Copy(salt, 0, hashBytes, 0, 16);
        Array.Copy(hash, 0, hashBytes, 16, 32);

        return await Task.FromResult(CurrentHashPrefix + Convert.ToBase64String(hashBytes));
    }

    public async Task<bool> VerifyPasswordAsync(string password, string hashedPassword)
    {
        try
        {
            // Detect hash version and select iteration count accordingly
            int iterations;
            string base64Data;

            if (hashedPassword.StartsWith(CurrentHashPrefix, StringComparison.Ordinal))
            {
                iterations = CurrentIterations;
                base64Data = hashedPassword.Substring(CurrentHashPrefix.Length);
            }
            else
            {
                // Legacy hash without prefix — uses old iteration count
                iterations = LegacyIterations;
                base64Data = hashedPassword;
            }

            var hashBytes = Convert.FromBase64String(base64Data);

            // Extract the salt
            var salt = new ArraySegment<byte>(hashBytes, 0, 16).ToArray();

            // Extract the stored hash
            var storedHash = new ArraySegment<byte>(hashBytes, 16, 32).ToArray();

            // Hash the provided password with the same salt and iterations
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var testHash = pbkdf2.GetBytes(32);

            // Compare the hashes
            return await Task.FromResult(CryptographicOperations.FixedTimeEquals(storedHash, testHash));
        }
        catch
        {
            return false;
        }
    }

    public bool NeedsRehash(string hashedPassword)
    {
        return !hashedPassword.StartsWith(CurrentHashPrefix, StringComparison.Ordinal);
    }

    public string GenerateSecurityStamp()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public async Task<AuthenticationResponse> GoogleLoginAsync(string email, string? firstName, string? lastName, string? googleId, string? inviteCode = null)
    {
        try
        {
            const string defaultIpAddress = "0.0.0.0"; // Default for OAuth flows
            
            // Check if user exists by email
            var existingUser = await _userRepository.GetByEmailAsync(email);
            
            // Also check if someone already has this email as their username
            if (existingUser == null)
            {
                var existingByUsername = await _userRepository.GetByUserNameAsync(email);
                if (existingByUsername != null)
                {
                    return new AuthenticationResponse
                    {
                        IsSuccess = false,
                        Errors = new List<string> { "An account with this email already exists. Please log in with your regular credentials." }
                    };
                }
            }
            
            if (existingUser != null)
            {
                // Update Google ID if not set
                if (string.IsNullOrEmpty(existingUser.GoogleId) && !string.IsNullOrEmpty(googleId))
                {
                    existingUser.GoogleId = googleId;
                    await _userRepository.UpdateAsync(existingUser);
                }

                var token = await GenerateJwtTokenAsync(existingUser);
                var refreshToken = await GenerateRefreshTokenAsync(existingUser, defaultIpAddress);

                return new AuthenticationResponse
                {
                    IsSuccess = true,
                    Token = token,
                    ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc),
                    RefreshToken = refreshToken.Token,
                    RefreshTokenExpiresAt = refreshToken.ExpiryDate,
                    User = new UserDto
                    {
                        Id = existingUser.Id,
                        Email = existingUser.Email,
                        UserName = existingUser.UserName,
                        FirstName = existingUser.FirstName,
                        LastName = existingUser.LastName,
                        FullName = $"{existingUser.FirstName} {existingUser.LastName}".Trim(),
                        Currency = existingUser.Currency,
                        TimeZone = existingUser.TimeZone,
                        AiDescriptionCleaning = existingUser.AiDescriptionCleaning
                    }
                };
            }

            // Validate invite code for new registrations if required
            if (_betaAccessOptions.RequireInviteCode)
            {
                var (isValid, errorMessage) = await _inviteCodeValidationService.ValidateAsync(inviteCode);
                if (!isValid)
                {
                    return new AuthenticationResponse
                    {
                        IsSuccess = false,
                        Errors = new List<string> { errorMessage ?? "A valid invite code is required to register during the beta period." }
                    };
                }
            }

            // Create new user
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = email, // Use email as username for Google users
                NormalizedUserName = email.ToUpperInvariant(),
                FirstName = firstName ?? "",
                LastName = lastName ?? "",
                GoogleId = googleId,
                EmailConfirmed = true, // Google users have verified emails
                PasswordHash = "", // No password for OAuth users
                SecurityStamp = GenerateSecurityStamp(),
                Currency = "USD", // Default currency
                TimeZone = "UTC", // Default timezone
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
            };

            try
            {
                await _userRepository.AddAsync(newUser);

                // Claim the invite code if one was provided
                if (!string.IsNullOrWhiteSpace(inviteCode))
                {
                    await _inviteCodeValidationService.ClaimAsync(inviteCode, newUser.Id);
                }

                // Seed default categories for new OAuth user
                await _categorySeedingService.CreateDefaultCategoriesAsync(newUser.Id);
                
                var newUserToken = await GenerateJwtTokenAsync(newUser);
                var newUserRefreshToken = await GenerateRefreshTokenAsync(newUser, defaultIpAddress);

                return new AuthenticationResponse
                {
                    IsSuccess = true,
                    Token = newUserToken,
                    ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc),
                    RefreshToken = newUserRefreshToken.Token,
                    RefreshTokenExpiresAt = newUserRefreshToken.ExpiryDate,
                    User = new UserDto
                    {
                        Id = newUser.Id,
                        Email = newUser.Email,
                        UserName = newUser.UserName,
                        FirstName = newUser.FirstName,
                        LastName = newUser.LastName,
                        FullName = $"{newUser.FirstName} {newUser.LastName}".Trim(),
                        Currency = newUser.Currency,
                        TimeZone = newUser.TimeZone,
                        AiDescriptionCleaning = newUser.AiDescriptionCleaning
                    }
                };
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
            {
                // Log the full exception details for debugging
                var innerMessage = dbEx.InnerException?.Message ?? "No inner exception";
                var fullMessage = $"Database error: {dbEx.Message}. Inner: {innerMessage}";
                
                Console.WriteLine($"[GOOGLE AUTH ERROR] {fullMessage}");
                Console.WriteLine($"[GOOGLE AUTH ERROR] Stack trace: {dbEx.StackTrace}");
                
                return new AuthenticationResponse
                {
                    IsSuccess = false,
                    Errors = new List<string> { fullMessage }
                };
            }
            catch (Exception innerEx)
            {
                // Log all other exceptions with full details
                Console.WriteLine($"[GOOGLE AUTH ERROR] General exception: {innerEx.Message}");
                Console.WriteLine($"[GOOGLE AUTH ERROR] Stack trace: {innerEx.StackTrace}");
                
                return new AuthenticationResponse
                {
                    IsSuccess = false,
                    Errors = new List<string> { $"Failed to create user account: {innerEx.Message}" }
                };
            }
        }
        catch (Exception ex)
        {
            // Log outer exception details
            Console.WriteLine($"[GOOGLE AUTH ERROR] Outer exception: {ex.Message}");
            Console.WriteLine($"[GOOGLE AUTH ERROR] Outer stack trace: {ex.StackTrace}");
            
            return new AuthenticationResponse
            {
                IsSuccess = false,
                Errors = new List<string> { $"Google authentication failed: {ex.Message}" }
            };
        }
    }

    public async Task<AuthenticationResponse> GoogleTokenLoginAsync(string idToken)
    {
        try
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            if (string.IsNullOrEmpty(clientId) ||
                string.Equals(clientId, "YOUR_GOOGLE_CLIENT_ID", StringComparison.Ordinal))
            {
                return new AuthenticationResponse
                {
                    IsSuccess = false,
                    Errors = new List<string> { "Google authentication not configured" }
                };
            }

            // Verify the Google ID token
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new[] { clientId }
            });

            return await GoogleLoginAsync(payload.Email, payload.GivenName, payload.FamilyName, payload.Subject);
        }
        catch (Exception ex)
        {
            return new AuthenticationResponse
            {
                IsSuccess = false,
                Errors = new List<string> { "Invalid Google token", ex.Message }
            };
        }
    }

    public async Task<RefreshToken> GenerateRefreshTokenAsync(User user, string ipAddress)
    {
        // Generate a cryptographically secure random token
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(randomBytes),
            ExpiryDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(30), DateTimeKind.Utc), // 30 days expiry
            UserId = user.Id,
            CreatedByIp = ipAddress,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
        };

        await _refreshTokenRepository.AddAsync(refreshToken);
        return refreshToken;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
    {
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(token);
        
        if (refreshToken == null || !refreshToken.IsActive)
        {
            return null;
        }

        return refreshToken;
    }

    public async Task<AuthenticationResponse> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        try
        {
            var token = await ValidateRefreshTokenAsync(refreshToken);
            
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
            var newRefreshToken = await GenerateRefreshTokenAsync(user, ipAddress);
            
            // Revoke the old refresh token
            token.Revoke(ipAddress, newRefreshToken.Token);
            await _refreshTokenRepository.UpdateAsync(token);

            return new AuthenticationResponse
            {
                IsSuccess = true,
                Token = newJwtToken,
                RefreshToken = newRefreshToken.Token,
                RefreshTokenExpiresAt = newRefreshToken.ExpiryDate,
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

    public async Task RevokeRefreshTokenAsync(string token, string ipAddress, string? replacedByToken = null)
    {
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(token);
        
        if (refreshToken != null && refreshToken.IsActive)
        {
            refreshToken.Revoke(ipAddress, replacedByToken);
            await _refreshTokenRepository.UpdateAsync(refreshToken);
        }
    }

    public async Task RevokeAllUserRefreshTokensAsync(Guid userId, string ipAddress)
    {
        await _refreshTokenRepository.RevokeAllUserTokensAsync(userId, ipAddress);
    }
}
