using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.Commands;
using MyMascada.Domain.Entities;
using NSubstitute;
using Xunit;

namespace MyMascada.Tests.Unit.Features.Authentication.Commands;

public class ResetPasswordCommandHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IValidator<ResetPasswordCommand> _validator;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;
    private readonly ResetPasswordCommandHandler _handler;

    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _tokenId = Guid.NewGuid();
    private const string TestEmail = "test@example.com";
    private const string TestRawToken = "test-raw-token-abc123";
    private const string NewPassword = "NewSecurePassword123!";

    public ResetPasswordCommandHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
        _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
        _authenticationService = Substitute.For<IAuthenticationService>();
        _validator = Substitute.For<IValidator<ResetPasswordCommand>>();
        _logger = Substitute.For<ILogger<ResetPasswordCommandHandler>>();

        _handler = new ResetPasswordCommandHandler(
            _userRepository,
            _tokenRepository,
            _refreshTokenRepository,
            _authenticationService,
            _validator,
            _logger);
    }

    [Fact]
    public async Task Handle_WithValidToken_UpdatesPasswordAndReturnsSuccess()
    {
        // Arrange
        var user = CreateTestUser();
        var token = CreateValidToken();
        SetupValidRequest();
        SetupTokenLookup(token);
        _userRepository.GetByIdAsync(_testUserId).Returns(user);
        _authenticationService.HashPasswordAsync(NewPassword).Returns("new-hashed-password");
        _authenticationService.GenerateSecurityStamp().Returns("new-security-stamp");

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("successfully");
        user.PasswordHash.Should().Be("new-hashed-password");
        user.SecurityStamp.Should().Be("new-security-stamp");
        await _userRepository.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ReturnsFailure()
    {
        // Arrange
        var token = CreateExpiredToken();
        SetupValidRequest();
        SetupTokenLookup(token);

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("expired"));
    }

    [Fact]
    public async Task Handle_WithUsedToken_ReturnsFailure()
    {
        // Arrange
        var token = CreateUsedToken();
        SetupValidRequest();
        SetupTokenLookup(token);

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("already been used"));
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ReturnsFailure()
    {
        // Arrange
        SetupValidRequest();
        _tokenRepository.GetByTokenHashAsync(Arg.Any<string>()).Returns((PasswordResetToken?)null);

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid or expired"));
    }

    [Fact]
    public async Task Handle_WithEmailMismatch_ReturnsFailure()
    {
        // Arrange
        var user = CreateTestUser();
        user.Email = "different@example.com"; // Different email
        var token = CreateValidToken();
        SetupValidRequest();
        SetupTokenLookup(token);
        _userRepository.GetByIdAsync(_testUserId).Returns(user);

        var command = CreateValidCommand(); // Has TestEmail

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid or expired"));
    }

    [Fact]
    public async Task Handle_RevokesAllRefreshTokensOnSuccess()
    {
        // Arrange
        var user = CreateTestUser();
        var token = CreateValidToken();
        SetupValidRequest();
        SetupTokenLookup(token);
        _userRepository.GetByIdAsync(_testUserId).Returns(user);
        _authenticationService.HashPasswordAsync(NewPassword).Returns("new-hash");
        _authenticationService.GenerateSecurityStamp().Returns("new-stamp");

        var command = new ResetPasswordCommand
        {
            Email = TestEmail,
            Token = TestRawToken,
            NewPassword = NewPassword,
            ConfirmPassword = NewPassword,
            IpAddress = "192.168.1.1"
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _refreshTokenRepository.Received(1).RevokeAllUserTokensAsync(user.Id, "192.168.1.1");
    }

    [Fact]
    public async Task Handle_MarksTokenAsUsedImmediately()
    {
        // Arrange
        var user = CreateTestUser();
        var token = CreateValidToken();
        SetupValidRequest();
        SetupTokenLookup(token);
        _userRepository.GetByIdAsync(_testUserId).Returns(user);
        _authenticationService.HashPasswordAsync(NewPassword).Returns("new-hash");
        _authenticationService.GenerateSecurityStamp().Returns("new-stamp");

        var command = CreateValidCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Token should be marked used BEFORE password update
        token.IsUsed.Should().BeTrue();
        token.UsedAt.Should().NotBeNull();
        Received.InOrder(async () =>
        {
            await _tokenRepository.UpdateAsync(token);
            await _userRepository.UpdateAsync(user);
        });
    }

    [Fact]
    public async Task Handle_InvalidatesAllOtherTokensOnSuccess()
    {
        // Arrange
        var user = CreateTestUser();
        var token = CreateValidToken();
        SetupValidRequest();
        SetupTokenLookup(token);
        _userRepository.GetByIdAsync(_testUserId).Returns(user);
        _authenticationService.HashPasswordAsync(NewPassword).Returns("new-hash");
        _authenticationService.GenerateSecurityStamp().Returns("new-stamp");

        var command = CreateValidCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _tokenRepository.Received(1).InvalidateAllForUserAsync(user.Id);
    }

    [Fact]
    public async Task Handle_WithValidationErrors_ReturnsFailure()
    {
        // Arrange
        _validator.ValidateAsync(Arg.Any<ResetPasswordCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("NewPassword", "Password too weak"),
                new ValidationFailure("ConfirmPassword", "Passwords don't match")
            }));

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Password too weak");
        result.Errors.Should().Contain("Passwords don't match");
    }

    [Fact]
    public async Task Handle_WithUserNotFound_ReturnsFailure()
    {
        // Arrange
        var token = CreateValidToken();
        SetupValidRequest();
        SetupTokenLookup(token);
        _userRepository.GetByIdAsync(_testUserId).Returns((User?)null);

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Invalid or expired"));
    }

    [Fact]
    public async Task Handle_UsesCorrectTokenHashForLookup()
    {
        // Arrange
        SetupValidRequest();
        _tokenRepository.GetByTokenHashAsync(Arg.Any<string>()).Returns((PasswordResetToken?)null);

        var command = CreateValidCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify the token was hashed using SHA-256
        var expectedHash = ComputeTokenHash(TestRawToken);
        await _tokenRepository.Received(1).GetByTokenHashAsync(expectedHash);
    }

    [Fact]
    public async Task Handle_DefaultsIpAddressToUnknown()
    {
        // Arrange
        var user = CreateTestUser();
        var token = CreateValidToken();
        SetupValidRequest();
        SetupTokenLookup(token);
        _userRepository.GetByIdAsync(_testUserId).Returns(user);
        _authenticationService.HashPasswordAsync(NewPassword).Returns("new-hash");
        _authenticationService.GenerateSecurityStamp().Returns("new-stamp");

        var command = new ResetPasswordCommand
        {
            Email = TestEmail,
            Token = TestRawToken,
            NewPassword = NewPassword,
            ConfirmPassword = NewPassword,
            IpAddress = null // No IP provided
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _refreshTokenRepository.Received(1).RevokeAllUserTokensAsync(user.Id, "unknown");
    }

    private User CreateTestUser()
    {
        return new User
        {
            Id = _testUserId,
            Email = TestEmail,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "old-hashed-password",
            SecurityStamp = "old-stamp"
        };
    }

    private PasswordResetToken CreateValidToken()
    {
        return new PasswordResetToken
        {
            Id = _tokenId,
            UserId = _testUserId,
            TokenHash = ComputeTokenHash(TestRawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            IsUsed = false,
            UsedAt = null
        };
    }

    private PasswordResetToken CreateExpiredToken()
    {
        return new PasswordResetToken
        {
            Id = _tokenId,
            UserId = _testUserId,
            TokenHash = ComputeTokenHash(TestRawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5), // Expired 5 minutes ago
            IsUsed = false,
            UsedAt = null
        };
    }

    private PasswordResetToken CreateUsedToken()
    {
        return new PasswordResetToken
        {
            Id = _tokenId,
            UserId = _testUserId,
            TokenHash = ComputeTokenHash(TestRawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            IsUsed = true,
            UsedAt = DateTime.UtcNow.AddMinutes(-10)
        };
    }

    private void SetupValidRequest()
    {
        _validator.ValidateAsync(Arg.Any<ResetPasswordCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
    }

    private void SetupTokenLookup(PasswordResetToken token)
    {
        _tokenRepository.GetByTokenHashAsync(ComputeTokenHash(TestRawToken)).Returns(token);
    }

    private ResetPasswordCommand CreateValidCommand()
    {
        return new ResetPasswordCommand
        {
            Email = TestEmail,
            Token = TestRawToken,
            NewPassword = NewPassword,
            ConfirmPassword = NewPassword
        };
    }

    private static string ComputeTokenHash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
