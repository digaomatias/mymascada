using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Extensions;
using System.Security.Claims;
using System.Text.Json;
using MyMascada.Application.Features.Authentication.Commands;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Application.Features.Authentication.Queries;
using MyMascada.Application.Common.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using MyMascada.WebAPI.Extensions;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAuthenticationService _authService;
    private readonly IDataProtector _dataProtector;
    private readonly IUserRepository _userRepository;
    private readonly MyMascada.Application.Common.Configuration.AppOptions _appOptions;
    private readonly IWebHostEnvironment _environment;
    private readonly IUserAiSettingsRepository _aiSettingsRepository;
    private readonly IConfiguration _configuration;
    private readonly IUserFinancialProfileRepository _financialProfileRepository;

    public AuthController(
        IMediator mediator,
        IAuthenticationService authService,
        IDataProtectionProvider dataProtectionProvider,
        IUserRepository userRepository,
        Microsoft.Extensions.Options.IOptions<MyMascada.Application.Common.Configuration.AppOptions> appOptions,
        IWebHostEnvironment environment,
        IUserAiSettingsRepository aiSettingsRepository,
        IConfiguration configuration,
        IUserFinancialProfileRepository financialProfileRepository)
    {
        _mediator = mediator;
        _authService = authService;
        _dataProtector = dataProtectionProvider.CreateProtector("OAuthState");
        _userRepository = userRepository;
        _appOptions = appOptions.Value;
        _environment = environment;
        _aiSettingsRepository = aiSettingsRepository;
        _configuration = configuration;
        _financialProfileRepository = financialProfileRepository;
    }

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.Authentication)]
    public async Task<ActionResult<AuthenticationResponse>> Register([FromBody] RegisterRequest request)
    {
        var command = new RegisterCommand
        {
            Email = request.Email,
            UserName = request.UserName,
            Password = request.Password,
            ConfirmPassword = request.ConfirmPassword,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Currency = request.Currency,
            TimeZone = request.TimeZone,
            InviteCode = request.InviteCode,
            IpAddress = GetClientIpAddress(),
            UserAgent = Request.Headers.UserAgent.FirstOrDefault()
        };

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            // If email verification is required, don't set any cookies
            if (result.RequiresEmailVerification)
            {
                var verificationResponse = new AuthenticationResponse
                {
                    IsSuccess = result.IsSuccess,
                    RequiresEmailVerification = true,
                    Message = result.Message,
                    User = result.User,
                    Errors = result.Errors
                };

                return Ok(verificationResponse);
            }

            // Set refresh token cookie if available (for users who don't need verification)
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt ?? DateTime.UtcNow.AddDays(30));
            }

            // Return response without refresh token (it's in cookie)
            var response = new AuthenticationResponse
            {
                IsSuccess = result.IsSuccess,
                Token = result.Token,
                ExpiresAt = result.ExpiresAt,
                User = result.User,
                Errors = result.Errors
            };

            return Ok(response);
        }

        return BadRequest(result);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.Authentication)]
    public async Task<ActionResult<AuthenticationResponse>> Login([FromBody] LoginRequest request)
    {
        var query = new LoginQuery
        {
            EmailOrUserName = request.EmailOrUserName,
            Password = request.Password,
            RememberMe = request.RememberMe
        };

        var result = await _mediator.Send(query);

        // If email verification is required, return 200 OK with verification flag
        if (result.RequiresEmailVerification)
        {
            var verificationResponse = new AuthenticationResponse
            {
                IsSuccess = false,
                RequiresEmailVerification = true,
                Message = result.Message,
                User = result.User,
                Errors = result.Errors
            };

            return Ok(verificationResponse);
        }

        if (result.IsSuccess)
        {
            // Set refresh token cookie if available
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt ?? DateTime.UtcNow.AddDays(30));
            }

            // Return response without refresh token (it's in cookie)
            var response = new AuthenticationResponse
            {
                IsSuccess = result.IsSuccess,
                Token = result.Token,
                ExpiresAt = result.ExpiresAt,
                User = result.User,
                Errors = result.Errors
            };

            return Ok(response);
        }

        return BadRequest(result);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        // Fetch user from database to get all fields including locale
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        // Check if user has AI configured (own key or global key)
        var aiSettings = await _aiSettingsRepository.GetByUserIdAsync(userId);
        var globalApiKey = _configuration["LLM:OpenAI:ApiKey"];
        var hasAiConfigured = (aiSettings != null && !string.IsNullOrEmpty(aiSettings.EncryptedApiKey))
            || (!string.IsNullOrEmpty(globalApiKey) && globalApiKey != "YOUR_OPENAI_API_KEY");

        // Check onboarding status
        var financialProfile = await _financialProfileRepository.GetByUserIdAsync(userId);
        var isOnboardingComplete = financialProfile != null && financialProfile.OnboardingCompleted;

        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            UserName = user.UserName ?? "",
            FirstName = user.FirstName ?? "",
            LastName = user.LastName ?? "",
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Currency = user.Currency ?? "NZD",
            TimeZone = user.TimeZone ?? "UTC",
            Locale = user.Locale ?? "en",
            AiDescriptionCleaning = user.AiDescriptionCleaning,
            HasAiConfigured = hasAiConfigured,
            IsOnboardingComplete = isOnboardingComplete
        };

        return Ok(userDto);
    }

    [HttpPatch("locale")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateLocale([FromBody] UpdateLocaleRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        // Validate the locale
        var supportedLocales = new[] { "en", "pt-BR" };
        if (!supportedLocales.Contains(request.Locale))
        {
            return BadRequest(new { Error = $"Unsupported locale. Supported locales: {string.Join(", ", supportedLocales)}" });
        }

        user.Locale = request.Locale;
        await _userRepository.UpdateAsync(user);

        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            UserName = user.UserName ?? "",
            FirstName = user.FirstName ?? "",
            LastName = user.LastName ?? "",
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Currency = user.Currency ?? "NZD",
            TimeZone = user.TimeZone ?? "UTC",
            Locale = user.Locale ?? "en",
            AiDescriptionCleaning = user.AiDescriptionCleaning
        };

        return Ok(userDto);
    }

    [HttpPatch("ai-description-cleaning")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateAiDescriptionCleaning([FromBody] UpdateAiDescriptionCleaningRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        user.AiDescriptionCleaning = request.Enabled;
        await _userRepository.UpdateAsync(user);

        var userDto = new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            UserName = user.UserName ?? "",
            FirstName = user.FirstName ?? "",
            LastName = user.LastName ?? "",
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Currency = user.Currency ?? "NZD",
            TimeZone = user.TimeZone ?? "UTC",
            Locale = user.Locale ?? "en",
            AiDescriptionCleaning = user.AiDescriptionCleaning
        };

        return Ok(userDto);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken()
    {
        try
        {
            // Get refresh token from httpOnly cookie
            var refreshToken = Request.Cookies["refresh_token"];
            
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized(new { Message = "Refresh token not found" });
            }

            // Get client IP address
            var ipAddress = GetClientIpAddress();

            // Refresh the tokens
            var result = await _authService.RefreshTokenAsync(refreshToken, ipAddress);

            if (!result.IsSuccess)
            {
                return Unauthorized(result);
            }

            // Set new refresh token in httpOnly cookie
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt ?? DateTime.UtcNow.AddDays(30));
            }

            // Return response without refresh token (it's in cookie)
            var response = new AuthenticationResponse
            {
                IsSuccess = result.IsSuccess,
                Token = result.Token,
                ExpiresAt = result.ExpiresAt,
                User = result.User,
                Errors = result.Errors
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while refreshing the token", Details = ex.Message });
        }
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> RevokeToken()
    {
        try
        {
            var refreshToken = Request.Cookies["refresh_token"];
            
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var ipAddress = GetClientIpAddress();
                await _authService.RevokeRefreshTokenAsync(refreshToken, ipAddress);
            }

            // Clear the refresh token cookie
            Response.Cookies.Delete("refresh_token");

            return Ok(new { Message = "Token revoked successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "An error occurred while revoking the token", Details = ex.Message });
        }
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.Authentication)]
    public async Task<ActionResult<PasswordResetResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var command = new ForgotPasswordCommand
        {
            Email = request.Email,
            IpAddress = GetClientIpAddress(),
            UserAgent = Request.Headers.UserAgent.FirstOrDefault()
        };

        var result = await _mediator.Send(command);

        // Always return 200 OK with the response to prevent user enumeration
        return Ok(result);
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.Authentication)]
    public async Task<ActionResult<PasswordResetResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand
        {
            Email = request.Email,
            Token = request.Token,
            NewPassword = request.NewPassword,
            ConfirmPassword = request.ConfirmPassword,
            IpAddress = GetClientIpAddress()
        };

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<PasswordResetResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var command = new ChangePasswordCommand
        {
            UserId = userId,
            CurrentPassword = request.CurrentPassword,
            NewPassword = request.NewPassword,
            ConfirmPassword = request.ConfirmPassword,
            IpAddress = GetClientIpAddress()
        };

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            // Clear the refresh token cookie since all sessions are invalidated
            Response.Cookies.Delete("refresh_token");
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPost("confirm-email")]
    public async Task<ActionResult<ConfirmEmailResult>> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        var command = new ConfirmEmailCommand
        {
            Email = request.Email,
            Token = request.Token
        };

        var result = await _mediator.Send(command);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    [HttpPost("resend-verification")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.Authentication)]
    public async Task<ActionResult<ResendVerificationEmailResult>> ResendVerificationEmail([FromBody] ResendVerificationRequest request)
    {
        var command = new ResendVerificationEmailCommand
        {
            Email = request.Email,
            IpAddress = GetClientIpAddress(),
            UserAgent = Request.Headers.UserAgent.FirstOrDefault()
        };

        var result = await _mediator.Send(command);

        // Always return 200 OK to prevent user enumeration
        return Ok(result);
    }

    [HttpGet("google-login-url")]
    public IActionResult GetGoogleLoginUrl(string? returnUrl = null, string? inviteCode = null)
    {
        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var clientId = configuration["Authentication:Google:ClientId"];

        if (string.IsNullOrEmpty(clientId) ||
            string.Equals(clientId, "YOUR_GOOGLE_CLIENT_ID", StringComparison.Ordinal))
        {
            return BadRequest("Google Client ID not configured");
        }

        // Validate returnUrl against allowed frontend origin to prevent open redirects
        var safeReturnUrl = $"{_appOptions.FrontendUrl}/dashboard";
        if (!string.IsNullOrEmpty(returnUrl))
        {
            var frontendUri = new Uri(_appOptions.FrontendUrl);
            if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsedReturn)
                && string.Equals(parsedReturn.Host, frontendUri.Host, StringComparison.OrdinalIgnoreCase)
                && string.Equals(parsedReturn.Scheme, frontendUri.Scheme, StringComparison.OrdinalIgnoreCase)
                && parsedReturn.Port == frontendUri.Port)
            {
                safeReturnUrl = returnUrl;
            }
        }

        // Create a state payload
        var statePayload = new
        {
            Nonce = Guid.NewGuid().ToString("N"),
            ReturnUrl = safeReturnUrl,
            InviteCode = inviteCode
        };

        // Protect the payload
        var protectedState = _dataProtector.Protect(JsonSerializer.Serialize(statePayload));

        var redirectUri = $"https://{Request.Host}/api/auth/google-response";
        var authorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        var queryParams = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "redirect_uri", redirectUri },
            { "response_type", "code" },
            { "scope", "openid profile email" },
            { "state", protectedState },
            { "access_type", "offline" },
            { "prompt", "consent" }
        };
        var googleRedirectUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(authorizationEndpoint, queryParams);

        return Ok(new { RedirectUrl = googleRedirectUrl });
    }


    [HttpGet("google-response")]
    [HttpPost("google-response")] // Also handle POST requests
    public async Task<IActionResult> GoogleResponse()
    {
        var stateFromRequest = Request.Query["state"].ToString();
        string? unprotectedState = null;
        
        try
        {
            unprotectedState = _dataProtector.Unprotect(stateFromRequest);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return BadRequest("Invalid or tampered state.");
        }

        if (unprotectedState == null)
        {
            return BadRequest("State could not be unprotected.");
        }

        var statePayload = JsonSerializer.Deserialize<JsonElement>(unprotectedState);
        var finalReturnUrl = statePayload.GetProperty("ReturnUrl").GetString();
        string? inviteCode = null;
        if (statePayload.TryGetProperty("InviteCode", out var inviteCodeElement) && inviteCodeElement.ValueKind != JsonValueKind.Null)
        {
            inviteCode = inviteCodeElement.GetString();
        }

        try
        {
            // Get the authorization code from the callback
            var code = Request.Query["code"].ToString();
            if (string.IsNullOrEmpty(code))
            {
                return BadRequest("Authorization code not provided by Google.");
            }

            // Exchange code for access token
            var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var clientId = configuration["Authentication:Google:ClientId"];
            var clientSecret = configuration["Authentication:Google:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) ||
                string.IsNullOrEmpty(clientSecret) ||
                string.Equals(clientId, "YOUR_GOOGLE_CLIENT_ID", StringComparison.Ordinal) ||
                string.Equals(clientSecret, "YOUR_GOOGLE_CLIENT_SECRET", StringComparison.Ordinal))
            {
                return BadRequest("Google OAuth credentials not configured");
            }
            
            var tokenResponse = await ExchangeCodeForTokensAsync(code, clientId, clientSecret);
            
            if (tokenResponse == null)
            {
                return BadRequest("Failed to exchange authorization code for tokens.");
            }

            // Get user info from Google
            var userInfo = await GetGoogleUserInfoAsync(tokenResponse.access_token);
            if (userInfo == null)
            {
                return BadRequest("Failed to get user information from Google.");
            }

            var authResult = await _authService.GoogleLoginAsync(
                userInfo.email,
                userInfo.given_name,
                userInfo.family_name,
                userInfo.id,
                inviteCode);

            if (authResult.IsSuccess)
            {
                // Set refresh token cookie if available
                if (!string.IsNullOrEmpty(authResult.RefreshToken))
                {
                    SetRefreshTokenCookie(authResult.RefreshToken, authResult.RefreshTokenExpiresAt ?? DateTime.UtcNow.AddDays(30));
                }

                // Validate the final return URL against the allowed frontend origin
                var frontendBase = new Uri(_appOptions.FrontendUrl);
                var redirectTarget = $"{_appOptions.FrontendUrl}/dashboard";
                if (!string.IsNullOrEmpty(finalReturnUrl)
                    && Uri.TryCreate(finalReturnUrl, UriKind.Absolute, out var parsedReturn)
                    && string.Equals(parsedReturn.Host, frontendBase.Host, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(parsedReturn.Scheme, frontendBase.Scheme, StringComparison.OrdinalIgnoreCase)
                    && parsedReturn.Port == frontendBase.Port)
                {
                    redirectTarget = finalReturnUrl;
                }

                // Pass a short-lived encrypted auth code instead of the raw JWT
                // The frontend exchanges this code via the /api/auth/exchange-code endpoint
                var codePayload = JsonSerializer.Serialize(new
                {
                    Token = authResult.Token,
                    ExpiresAt = authResult.ExpiresAt,
                    CreatedAt = DateTime.UtcNow
                });
                var authCode = _dataProtector.Protect(codePayload);

                var separator = redirectTarget.Contains('?') ? "&" : "?";
                return Redirect($"{redirectTarget}{separator}code={Uri.EscapeDataString(authCode)}");
            }

            return BadRequest(authResult);
        }
        catch (Exception ex)
        {
            // TODO: Log the exception (ex) to a proper logging framework
            return StatusCode(500, "An unexpected error occurred during Google authentication.");
        }
    }

    [HttpPost("exchange-code")]
    public IActionResult ExchangeCode([FromBody] ExchangeCodeRequest request)
    {
        try
        {
            var json = _dataProtector.Unprotect(request.Code);
            var payload = JsonSerializer.Deserialize<JsonElement>(json);

            var createdAt = payload.GetProperty("CreatedAt").GetDateTime();
            if (DateTime.UtcNow - createdAt > TimeSpan.FromMinutes(2))
            {
                return BadRequest(new { Error = "Code expired" });
            }

            var token = payload.GetProperty("Token").GetString();
            var expiresAtElement = payload.GetProperty("ExpiresAt");
            var expiresAt = expiresAtElement.ValueKind != JsonValueKind.Null
                ? expiresAtElement.GetDateTime()
                : DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Utc);

            return Ok(new { Token = token, ExpiresAt = expiresAt });
        }
        catch
        {
            return BadRequest(new { Error = "Invalid or expired code" });
        }
    }

    [HttpPost("google-token")]
    public async Task<ActionResult<AuthenticationResponse>> GoogleTokenLogin([FromBody] GoogleTokenRequest request)
    {
        try
        {
            // This endpoint handles Google Sign-In from the frontend
            var authResult = await _authService.GoogleTokenLoginAsync(request.IdToken);
            
            if (authResult.IsSuccess)
            {
                // Set refresh token cookie if available
                if (!string.IsNullOrEmpty(authResult.RefreshToken))
                {
                    SetRefreshTokenCookie(authResult.RefreshToken, authResult.RefreshTokenExpiresAt ?? DateTime.UtcNow.AddDays(30));
                }

                // Return response without refresh token (it's in cookie)
                var response = new AuthenticationResponse
                {
                    IsSuccess = authResult.IsSuccess,
                    Token = authResult.Token,
                    ExpiresAt = authResult.ExpiresAt,
                    User = authResult.User,
                    Errors = authResult.Errors
                };

                return Ok(response);
            }

            return BadRequest(authResult);
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = "Google token authentication failed", Details = ex.Message });
        }
    }


    // Catch-all for any Google auth routes that might be misconfigured
    // Only available in Development to prevent reflected XSS in production
    [HttpGet("google-{action}")]
    public IActionResult GoogleCatchAll(string action)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(new
        {
            Message = "Unrecognised Google auth route",
            Action = action,
            ExpectedEndpoint = "/api/auth/google-response",
            AvailableEndpoints = new[]
            {
                "/api/auth/google-login-url",
                "/api/auth/google-response",
                "/api/auth/google-token"
            }
        });
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expires)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps, // Use secure cookies only over HTTPS
            SameSite = SameSiteMode.Lax,
            Expires = expires
        };

        Response.Cookies.Append("refresh_token", refreshToken, cookieOptions);
    }

    private string GetClientIpAddress()
    {
        // Try to get the real IP address from forwarded headers (for reverse proxy scenarios)
        var xForwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
            return xForwardedFor.Split(',')[0].Trim();
        }

        var xRealIp = Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp))
        {
            return xRealIp;
        }

        // Fallback to connection remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task<TokenResponse?> ExchangeCodeForTokensAsync(string code, string clientId, string clientSecret)
    {
        var tokenEndpoint = "https://oauth2.googleapis.com/token";
        var redirectUri = $"https://{Request.Host}/api/auth/google-response";

        var tokenRequest = new Dictionary<string, string>
        {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", redirectUri }
        };

        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(tokenRequest));

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(responseContent, options);
    }

    private async Task<GoogleUserInfo?> GetGoogleUserInfoAsync(string accessToken)
    {
        var userInfoEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
        
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        var response = await httpClient.GetAsync(userInfoEndpoint);
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        return System.Text.Json.JsonSerializer.Deserialize<GoogleUserInfo>(responseContent, options);
    }
}

public class GoogleTokenRequest
{
    public string IdToken { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string access_token { get; set; } = string.Empty;
    public string token_type { get; set; } = string.Empty;
    public int expires_in { get; set; }
    public string refresh_token { get; set; } = string.Empty;
    public string scope { get; set; } = string.Empty;
}

public class GoogleUserInfo
{
    public string id { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string given_name { get; set; } = string.Empty;
    public string family_name { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string picture { get; set; } = string.Empty;
}

public class UpdateLocaleRequest
{
    public string Locale { get; set; } = "en";
}

public class ConfirmEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class ResendVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ExchangeCodeRequest
{
    public string Code { get; set; } = string.Empty;
}

public class UpdateAiDescriptionCleaningRequest
{
    public bool Enabled { get; set; }
}
