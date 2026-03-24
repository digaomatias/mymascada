using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.Commands;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Domain.Entities;
using NSubstitute;

namespace MyMascada.Tests.Unit.Handlers;

public class RegisterCommandHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthenticationService _authService;
    private readonly IValidator<RegisterCommand> _validator;
    private readonly IRegistrationStrategy _registrationStrategy;
    private readonly IInviteCodeValidationService _inviteCodeValidationService;
    private readonly IBillingService _billingService;
    private readonly IFeatureFlags _featureFlags;
    private readonly ILogger<RegisterCommandHandler> _logger;
    private readonly BetaAccessOptions _betaAccessOptions;

    public RegisterCommandHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _authService = Substitute.For<IAuthenticationService>();
        _validator = Substitute.For<IValidator<RegisterCommand>>();
        _registrationStrategy = Substitute.For<IRegistrationStrategy>();
        _inviteCodeValidationService = Substitute.For<IInviteCodeValidationService>();
        _billingService = Substitute.For<IBillingService>();
        _featureFlags = Substitute.For<IFeatureFlags>();
        _logger = Substitute.For<ILogger<RegisterCommandHandler>>();
        _betaAccessOptions = new BetaAccessOptions();

        // Default: validation passes
        _validator.ValidateAsync(Arg.Any<RegisterCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        // Default: no duplicate users
        _userRepository.ExistsByEmailAsync(Arg.Any<string>()).Returns(false);
        _userRepository.ExistsByUserNameAsync(Arg.Any<string>()).Returns(false);

        // Default: auth service stubs
        _authService.HashPasswordAsync(Arg.Any<string>()).Returns("hashed");
        _authService.GenerateSecurityStamp().Returns(Guid.NewGuid().ToString());

        // Default: registration strategy returns success
        _registrationStrategy.AutoConfirmEmail.Returns(true);
        _registrationStrategy.CompleteRegistrationAsync(Arg.Any<User>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AuthenticationResponse { IsSuccess = true });
    }

    private RegisterCommandHandler CreateHandler() =>
        new(_userRepository, _authService, _validator, _registrationStrategy,
            _inviteCodeValidationService, _billingService, _featureFlags, _logger,
            Options.Create(_betaAccessOptions));

    private static RegisterCommand CreateValidCommand(string? clientPlatform = null, string? inviteCode = null) =>
        new()
        {
            Email = "test@example.com",
            UserName = "testuser",
            Password = "TestPass123!",
            ConfirmPassword = "TestPass123!",
            FirstName = "Test",
            LastName = "User",
            Currency = "USD",
            TimeZone = "UTC",
            ClientPlatform = clientPlatform,
            InviteCode = inviteCode
        };

    [Fact]
    public async Task Handle_MobileClient_WithNoInviteCode_WhenBypassEnabled_ShouldSucceed()
    {
        // Arrange
        _betaAccessOptions.RequireInviteCode = true;
        _betaAccessOptions.MobileBypassEnabled = true;

        var handler = CreateHandler();
        var command = CreateValidCommand(clientPlatform: RegisterCommandHandler.MobileClientPlatform);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _inviteCodeValidationService.DidNotReceive()
            .ValidateAsync(Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_WebClient_WithNoInviteCode_ShouldFail()
    {
        // Arrange
        _betaAccessOptions.RequireInviteCode = true;
        _betaAccessOptions.MobileBypassEnabled = true;

        _inviteCodeValidationService.ValidateAsync(Arg.Any<string?>())
            .Returns((false, "A valid invite code is required to register during the beta period."));

        var handler = CreateHandler();
        var command = CreateValidCommand(); // no client platform, no invite code

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("invite code"));
    }

    [Fact]
    public async Task Handle_MobileClient_WhenBypassDisabled_WithNoInviteCode_ShouldFail()
    {
        // Arrange
        _betaAccessOptions.RequireInviteCode = true;
        _betaAccessOptions.MobileBypassEnabled = false;

        _inviteCodeValidationService.ValidateAsync(Arg.Any<string?>())
            .Returns((false, "A valid invite code is required to register during the beta period."));

        var handler = CreateHandler();
        var command = CreateValidCommand(clientPlatform: RegisterCommandHandler.MobileClientPlatform);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("invite code"));
    }

    [Fact]
    public async Task Handle_WebClient_WithValidInviteCode_ShouldSucceed()
    {
        // Arrange
        _betaAccessOptions.RequireInviteCode = true;
        _betaAccessOptions.MobileBypassEnabled = true;

        _inviteCodeValidationService.ValidateAsync("VALID-CODE")
            .Returns((true, (string?)null));

        var handler = CreateHandler();
        var command = CreateValidCommand(inviteCode: "VALID-CODE");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
