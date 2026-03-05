using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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
    private readonly ICategorySeedingService _categorySeedingService;
    private readonly IInviteCodeValidationService _inviteCodeValidationService;
    private readonly ITokenService _tokenService;
    private readonly BetaAccessOptions _betaAccessOptions;

    public AuthenticationService(
        IConfiguration configuration,
        IUserRepository userRepository,
        ICategorySeedingService categorySeedingService,
        IInviteCodeValidationService inviteCodeValidationService,
        ITokenService tokenService,
        IOptions<BetaAccessOptions> betaAccessOptions)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _categorySeedingService = categorySeedingService;
        _inviteCodeValidationService = inviteCodeValidationService;
        _tokenService = tokenService;
        _betaAccessOptions = betaAccessOptions.Value;
    }

    private const int CurrentIterations = 600000;
    private const int LegacyIterations = 10000;
    private const string CurrentHashPrefix = "v2$";

    public Task<string> HashPasswordAsync(string password)
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

        return Task.FromResult(CurrentHashPrefix + Convert.ToBase64String(hashBytes));
    }

    public Task<bool> VerifyPasswordAsync(string password, string hashedPassword)
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
            return Task.FromResult(CryptographicOperations.FixedTimeEquals(storedHash, testHash));
        }
        catch
        {
            return Task.FromResult(false);
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

                var token = await _tokenService.GenerateJwtTokenAsync(existingUser);
                var refreshTokenResult = await _tokenService.GenerateRefreshTokenAsync(existingUser, defaultIpAddress);

                return new AuthenticationResponse
                {
                    IsSuccess = true,
                    Token = token,
                    ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc),
                    RefreshToken = refreshTokenResult.RawToken,
                    RefreshTokenExpiresAt = refreshTokenResult.ExpiresAt,
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

                var newUserToken = await _tokenService.GenerateJwtTokenAsync(newUser);
                var newUserRefreshTokenResult = await _tokenService.GenerateRefreshTokenAsync(newUser, defaultIpAddress);

                return new AuthenticationResponse
                {
                    IsSuccess = true,
                    Token = newUserToken,
                    ExpiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc),
                    RefreshToken = newUserRefreshTokenResult.RawToken,
                    RefreshTokenExpiresAt = newUserRefreshTokenResult.ExpiresAt,
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

    public async Task<AuthenticationResponse> RefreshTokenAsync(string rawToken, string ipAddress)
    {
        return await _tokenService.RefreshTokenAsync(rawToken, ipAddress);
    }

    public async Task RevokeRefreshTokenAsync(string rawToken, string ipAddress)
    {
        await _tokenService.RevokeRefreshTokenAsync(rawToken, ipAddress);
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
}
