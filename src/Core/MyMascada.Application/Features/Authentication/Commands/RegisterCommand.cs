using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Authentication.Commands;

public class RegisterCommand : IRequest<AuthenticationResponse>
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Currency { get; set; } = "USD";
    public string TimeZone { get; set; } = "UTC";
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? InviteCode { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthenticationResponse>
{
    private static readonly string[] SupportedLocales = ["en", "pt-BR"];
    private static readonly HashSet<string> ValidCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AR", "AU", "BR", "CA", "CL", "CO", "DE", "ES", "FR", "GB", "JP", "MX", "NZ", "PT", "US"
    };

    private readonly IUserRepository _userRepository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IValidator<RegisterCommand> _validator;
    private readonly IRegistrationStrategy _registrationStrategy;
    private readonly IInviteCodeValidationService _inviteCodeValidationService;
    private readonly IBillingService _billingService;
    private readonly IFeatureFlags _featureFlags;
    private readonly ILogger<RegisterCommandHandler> _logger;
    private readonly BetaAccessOptions _betaAccessOptions;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IAuthenticationService authenticationService,
        IValidator<RegisterCommand> validator,
        IRegistrationStrategy registrationStrategy,
        IInviteCodeValidationService inviteCodeValidationService,
        IBillingService billingService,
        IFeatureFlags featureFlags,
        ILogger<RegisterCommandHandler> logger,
        IOptions<BetaAccessOptions> betaAccessOptions)
    {
        _userRepository = userRepository;
        _authenticationService = authenticationService;
        _validator = validator;
        _registrationStrategy = registrationStrategy;
        _inviteCodeValidationService = inviteCodeValidationService;
        _billingService = billingService;
        _featureFlags = featureFlags;
        _logger = logger;
        _betaAccessOptions = betaAccessOptions.Value;
    }

    public async Task<AuthenticationResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var response = new AuthenticationResponse();

        // Validate the command using FluentValidation
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            response.Errors.AddRange(validationResult.Errors.Select(e => e.ErrorMessage));
            return response;
        }

        // Validate invite code if required
        if (_betaAccessOptions.RequireInviteCode)
        {
            var (isValid, errorMessage) = await _inviteCodeValidationService.ValidateAsync(request.InviteCode);
            if (!isValid)
            {
                _logger.LogWarning("Registration attempted with invalid invite code: {InviteCode}", request.InviteCode ?? "(empty)");
                response.Errors.Add(errorMessage ?? "A valid invite code is required to register during the beta period.");
                return response;
            }
        }

        // Check if email already exists
        if (await _userRepository.ExistsByEmailAsync(request.Email))
        {
            response.Errors.Add("Email is already registered");
            return response;
        }

        // Check if username already exists
        if (await _userRepository.ExistsByUserNameAsync(request.UserName))
        {
            response.Errors.Add("Username is already taken");
            return response;
        }

        // Resolve locale from language preference — use canonical casing from the allowlist
        var locale = SupportedLocales.FirstOrDefault(l =>
            string.Equals(l, request.Language, StringComparison.OrdinalIgnoreCase)) ?? "en";

        // Sanitize country code — only accept known codes, otherwise store null
        var country = !string.IsNullOrWhiteSpace(request.Country) &&
                      ValidCountryCodes.Contains(request.Country.Trim().ToUpperInvariant())
            ? request.Country.Trim().ToUpperInvariant()
            : null;

        // Create new user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            NormalizedEmail = request.Email.ToUpperInvariant(),
            UserName = request.UserName,
            NormalizedUserName = request.UserName.ToUpperInvariant(),
            PasswordHash = await _authenticationService.HashPasswordAsync(request.Password),
            SecurityStamp = _authenticationService.GenerateSecurityStamp(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Currency = request.Currency,
            Locale = locale,
            TimeZone = request.TimeZone,
            EmailConfirmed = _registrationStrategy.AutoConfirmEmail,
            PhoneNumberConfirmed = false,
            TwoFactorEnabled = false,
            LockoutEnabled = true,
            AccessFailedCount = 0
        };

        // Save user
        await _userRepository.AddAsync(user);

        // Claim the invite code if one was provided
        if (!string.IsNullOrWhiteSpace(request.InviteCode))
        {
            await _inviteCodeValidationService.ClaimAsync(request.InviteCode, user.Id);
        }

        // Create Stripe customer and assign free plan when billing is enabled
        if (_featureFlags.StripeBilling)
        {
            try
            {
                await _billingService.CreateCustomerAsync(user.Id, user.Email, user.FullName);
                _logger.LogInformation("Stripe customer created for user {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Stripe customer for user {UserId}. Registration continues.", user.Id);
            }
        }

        // Note: Categories are no longer auto-seeded at registration.
        // Users seed default categories from Settings with their preferred locale.

        // Delegate post-creation flow to the strategy (email verification or direct JWT)
        return await _registrationStrategy.CompleteRegistrationAsync(
            user, request.IpAddress, request.UserAgent, cancellationToken);
    }
}
