using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.Commands;
using MyMascada.Application.Features.Email.DTOs;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Authentication.Commands;

public class ForgotPasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IEmailServiceFactory _emailServiceFactory;
    private readonly IEmailService _emailService;
    private readonly IValidator<ForgotPasswordCommand> _validator;
    private readonly IOptions<PasswordResetOptions> _options;
    private readonly IOptions<AppOptions> _appOptions;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;
    private readonly ForgotPasswordCommandHandler _handler;

    private readonly Guid _testUserId = Guid.NewGuid();
    private const string TestEmail = "test@example.com";
    private const string GenericMessage = "If your email address is registered with us, you will receive a password reset link shortly.";

    public ForgotPasswordCommandHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
        _emailServiceFactory = Substitute.For<IEmailServiceFactory>();
        _emailService = Substitute.For<IEmailService>();
        _validator = Substitute.For<IValidator<ForgotPasswordCommand>>();
        _logger = Substitute.For<ILogger<ForgotPasswordCommandHandler>>();

        var passwordResetOptions = new PasswordResetOptions
        {
            TokenExpirationMinutes = 30,
            MaxRequestsPerHour = 3,
            FrontendResetUrl = "https://app.test.com/auth/reset-password"
        };
        _options = Options.Create(passwordResetOptions);

        var appOptions = new AppOptions
        {
            FrontendUrl = "https://app.test.com"
        };
        _appOptions = Options.Create(appOptions);

        _emailServiceFactory.GetDefaultProvider().Returns(_emailService);

        _handler = new ForgotPasswordCommandHandler(
            _userRepository,
            _tokenRepository,
            _emailServiceFactory,
            _validator,
            _options,
            _appOptions,
            _logger);
    }

    [Fact]
    public async Task Handle_WithValidEmail_SendsEmailAndReturnsSuccess()
    {
        // Arrange
        var user = CreateTestUser();
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns(user);
        _tokenRepository.GetRecentRequestCountAsync(user.Id, Arg.Any<TimeSpan>()).Returns(0);
        _emailService.SendTemplateAsync(Arg.Any<TemplatedEmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));

        var command = new ForgotPasswordCommand { Email = TestEmail };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(GenericMessage);
        await _emailService.Received(1).SendTemplateAsync(
            Arg.Is<TemplatedEmailMessage>(m => m.To == TestEmail && m.TemplateName == "password-reset"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNonExistentEmail_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange - This prevents user enumeration attacks
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns((User?)null);

        var command = new ForgotPasswordCommand { Email = TestEmail };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(GenericMessage);
        await _emailService.DidNotReceive().SendTemplateAsync(
            Arg.Any<TemplatedEmailMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRateLimited_ReturnsSuccessWithoutSendingEmail()
    {
        // Arrange - Rate limited but still returns success (no enumeration)
        var user = CreateTestUser();
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns(user);
        _tokenRepository.GetRecentRequestCountAsync(user.Id, Arg.Any<TimeSpan>()).Returns(3); // At limit

        var command = new ForgotPasswordCommand { Email = TestEmail };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(GenericMessage);
        await _emailService.DidNotReceive().SendTemplateAsync(
            Arg.Any<TemplatedEmailMessage>(),
            Arg.Any<CancellationToken>());
        await _tokenRepository.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>());
    }

    [Fact]
    public async Task Handle_StoresHashedTokenNotPlaintext()
    {
        // Arrange
        var user = CreateTestUser();
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns(user);
        _tokenRepository.GetRecentRequestCountAsync(user.Id, Arg.Any<TimeSpan>()).Returns(0);
        _emailService.SendTemplateAsync(Arg.Any<TemplatedEmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));

        PasswordResetToken? capturedToken = null;
        await _tokenRepository.AddAsync(Arg.Do<PasswordResetToken>(t => capturedToken = t));

        var command = new ForgotPasswordCommand { Email = TestEmail };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedToken.Should().NotBeNull();
        // Token hash should be 64 characters (SHA-256 hex string)
        capturedToken!.TokenHash.Should().HaveLength(64);
        // Token hash should only contain hex characters
        capturedToken.TokenHash.Should().MatchRegex("^[a-f0-9]+$");
    }

    [Fact]
    public async Task Handle_CreatesTokenWith30MinuteExpiration()
    {
        // Arrange
        var user = CreateTestUser();
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns(user);
        _tokenRepository.GetRecentRequestCountAsync(user.Id, Arg.Any<TimeSpan>()).Returns(0);
        _emailService.SendTemplateAsync(Arg.Any<TemplatedEmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));

        PasswordResetToken? capturedToken = null;
        await _tokenRepository.AddAsync(Arg.Do<PasswordResetToken>(t => capturedToken = t));

        var beforeRequest = DateTime.UtcNow;
        var command = new ForgotPasswordCommand { Email = TestEmail };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedToken.Should().NotBeNull();
        capturedToken!.ExpiresAt.Should().BeCloseTo(beforeRequest.AddMinutes(30), TimeSpan.FromSeconds(5));
        capturedToken.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_InvalidatesExistingTokensBeforeCreatingNew()
    {
        // Arrange
        var user = CreateTestUser();
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns(user);
        _tokenRepository.GetRecentRequestCountAsync(user.Id, Arg.Any<TimeSpan>()).Returns(0);
        _emailService.SendTemplateAsync(Arg.Any<TemplatedEmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));

        var command = new ForgotPasswordCommand { Email = TestEmail };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - InvalidateAllForUserAsync should be called BEFORE AddAsync
        Received.InOrder(() =>
        {
            _tokenRepository.InvalidateAllForUserAsync(user.Id);
            _tokenRepository.AddAsync(Arg.Any<PasswordResetToken>());
        });
    }

    [Fact]
    public async Task Handle_WithInvalidEmailFormat_ReturnsSuccessWithGenericMessage()
    {
        // Arrange - Invalid format but still generic message (no enumeration)
        _validator.ValidateAsync(Arg.Any<ForgotPasswordCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Email", "Invalid email format") }));

        var command = new ForgotPasswordCommand { Email = "not-an-email" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(GenericMessage);
    }

    [Fact]
    public async Task Handle_WhenEmailSendingFails_StillReturnsSuccess()
    {
        // Arrange - Email failure shouldn't leak info
        var user = CreateTestUser();
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns(user);
        _tokenRepository.GetRecentRequestCountAsync(user.Id, Arg.Any<TimeSpan>()).Returns(0);
        _emailService.SendTemplateAsync(Arg.Any<TemplatedEmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Failed("SMTP error", "SMTP_ERROR"));

        var command = new ForgotPasswordCommand { Email = TestEmail };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(GenericMessage);
    }

    [Fact]
    public async Task Handle_CapturesIpAddressAndUserAgent()
    {
        // Arrange
        var user = CreateTestUser();
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns(user);
        _tokenRepository.GetRecentRequestCountAsync(user.Id, Arg.Any<TimeSpan>()).Returns(0);
        _emailService.SendTemplateAsync(Arg.Any<TemplatedEmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));

        PasswordResetToken? capturedToken = null;
        await _tokenRepository.AddAsync(Arg.Do<PasswordResetToken>(t => capturedToken = t));

        var command = new ForgotPasswordCommand
        {
            Email = TestEmail,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0"
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedToken.Should().NotBeNull();
        capturedToken!.IpAddress.Should().Be("192.168.1.1");
        capturedToken.UserAgent.Should().Be("Mozilla/5.0");
    }

    [Fact]
    public async Task Handle_IncludesResetUrlInEmail()
    {
        // Arrange
        var user = CreateTestUser();
        SetupValidRequest();
        _userRepository.GetByEmailAsync(TestEmail).Returns(user);
        _tokenRepository.GetRecentRequestCountAsync(user.Id, Arg.Any<TimeSpan>()).Returns(0);

        TemplatedEmailMessage? capturedEmail = null;
        _emailService.SendTemplateAsync(Arg.Do<TemplatedEmailMessage>(m => capturedEmail = m), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));

        var command = new ForgotPasswordCommand { Email = TestEmail };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedEmail.Should().NotBeNull();
        capturedEmail!.TemplateData.Should().ContainKey("ResetUrl");
        var resetUrl = capturedEmail.TemplateData["ResetUrl"].ToString();
        resetUrl.Should().StartWith("https://app.test.com/auth/reset-password?token=");
        resetUrl.Should().Contain($"email={Uri.EscapeDataString(TestEmail)}");
    }

    private User CreateTestUser()
    {
        return new User
        {
            Id = _testUserId,
            Email = TestEmail,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hashed"
        };
    }

    private void SetupValidRequest()
    {
        _validator.ValidateAsync(Arg.Any<ForgotPasswordCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
    }
}
