using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Authentication.Queries;

public class LoginQuery : IRequest<AuthenticationResponse>
{
    public string EmailOrUserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}

public class LoginQueryHandler : IRequestHandler<LoginQuery, AuthenticationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthenticationService _authenticationService;

    public LoginQueryHandler(
        IUserRepository userRepository,
        IAuthenticationService authenticationService)
    {
        _userRepository = userRepository;
        _authenticationService = authenticationService;
    }

    public async Task<AuthenticationResponse> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        var response = new AuthenticationResponse();

        // Find user by email or username
        User? user = null;
        if (request.EmailOrUserName.Contains('@'))
        {
            user = await _userRepository.GetByEmailAsync(request.EmailOrUserName);
        }
        else
        {
            user = await _userRepository.GetByUserNameAsync(request.EmailOrUserName);
        }

        if (user == null)
        {
            response.Errors.Add("Invalid credentials");
            return response;
        }

        // Check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            response.Errors.Add($"Account is locked until {user.LockoutEnd.Value:yyyy-MM-dd HH:mm} UTC");
            return response;
        }

        // Verify password
        var isValidPassword = await _authenticationService.VerifyPasswordAsync(request.Password, user.PasswordHash);

        if (!isValidPassword)
        {
            // Increment failed login count
            user.AccessFailedCount++;

            // Lock account after 5 failed attempts
            if (user.AccessFailedCount >= 5 && user.LockoutEnabled)
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
                response.Errors.Add("Account has been locked due to multiple failed login attempts");
            }
            else
            {
                response.Errors.Add("Invalid credentials");
            }

            await _userRepository.UpdateAsync(user);
            return response;
        }

        // Check if email is verified
        // When email is not configured, users are created with EmailConfirmed = true
        // via DirectRegistrationStrategy, so this check never blocks self-hosted users.
        if (!user.EmailConfirmed)
        {
            response.RequiresEmailVerification = true;
            response.Message = "Please verify your email address before signing in. Check your inbox for the verification link.";
            response.User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Currency = user.Currency,
                TimeZone = user.TimeZone,
                ProfilePictureUrl = user.ProfilePictureUrl
            };
            return response;
        }

        // Reset failed login count
        if (user.AccessFailedCount > 0)
        {
            user.AccessFailedCount = 0;
            user.LockoutEnd = null;
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Generate JWT token and refresh token
        var token = await _authenticationService.GenerateJwtTokenAsync(user);
        var refreshToken = await _authenticationService.GenerateRefreshTokenAsync(user, "0.0.0.0"); // Default IP for command handler
        var expiresAt = request.RememberMe ? DateTime.UtcNow.AddDays(30) : DateTime.UtcNow.AddDays(7);

        response.IsSuccess = true;
        response.Token = token;
        response.ExpiresAt = expiresAt;
        response.RefreshToken = refreshToken.Token;
        response.RefreshTokenExpiresAt = refreshToken.ExpiryDate;
        response.User = new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            Currency = user.Currency,
            TimeZone = user.TimeZone,
            ProfilePictureUrl = user.ProfilePictureUrl
        };

        return response;
    }
}
