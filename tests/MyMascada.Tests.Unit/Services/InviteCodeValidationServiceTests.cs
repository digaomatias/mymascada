using MyMascada.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Services;

namespace MyMascada.Tests.Unit.Services;

public class InviteCodeValidationServiceTests
{
    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly ILogger<InviteCodeValidationService> _logger;
    private readonly IOptions<BetaAccessOptions> _options;
    private readonly InviteCodeValidationService _service;

    private const string TestCode = "MYMASC-ABC123";
    private static readonly Guid TestUserId = Guid.NewGuid();

    public InviteCodeValidationServiceTests()
    {
        _invitationCodeRepository = Substitute.For<IInvitationCodeRepository>();
        _waitlistRepository = Substitute.For<IWaitlistRepository>();
        _logger = Substitute.For<ILogger<InviteCodeValidationService>>();
        _options = Options.Create(new BetaAccessOptions
        {
            RequireInviteCode = true,
            ValidInviteCodes = "ENV-CODE1,ENV-CODE2"
        });

        _service = new InviteCodeValidationService(
            _invitationCodeRepository,
            _waitlistRepository,
            _options,
            _logger);
    }

    // --- ValidateAsync Tests ---

    [Fact]
    public async Task ValidateAsync_WithNullCode_ReturnsInvalid()
    {
        // Arrange
        string? code = null;

        // Act
        var (isValid, errorMessage) = await _service.ValidateAsync(code);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_WithEmptyCode_ReturnsInvalid(string code)
    {
        // Arrange / Act
        var (isValid, errorMessage) = await _service.ValidateAsync(code);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithValidDbCode_ReturnsValid()
    {
        // Arrange
        var invitationCode = CreateActiveInvitationCode();
        _invitationCodeRepository.GetByNormalizedCodeAsync(TestCode.ToUpperInvariant())
            .Returns(invitationCode);

        // Act
        var (isValid, errorMessage) = await _service.ValidateAsync(TestCode);

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_WithExpiredDbCode_ReturnsInvalid()
    {
        // Arrange
        var invitationCode = CreateActiveInvitationCode();
        invitationCode.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        _invitationCodeRepository.GetByNormalizedCodeAsync(TestCode.ToUpperInvariant())
            .Returns(invitationCode);

        // Act
        var (isValid, errorMessage) = await _service.ValidateAsync(TestCode);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("expired");
    }

    [Fact]
    public async Task ValidateAsync_WithUsedUpDbCode_ReturnsInvalid()
    {
        // Arrange
        var invitationCode = CreateActiveInvitationCode();
        invitationCode.MaxUses = 1;
        invitationCode.UseCount = 1;
        _invitationCodeRepository.GetByNormalizedCodeAsync(TestCode.ToUpperInvariant())
            .Returns(invitationCode);

        // Act
        var (isValid, errorMessage) = await _service.ValidateAsync(TestCode);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("already been used");
    }

    [Fact]
    public async Task ValidateAsync_WithRevokedDbCode_ReturnsInvalid()
    {
        // Arrange
        var invitationCode = CreateActiveInvitationCode();
        invitationCode.Status = InvitationCodeStatus.Revoked;
        _invitationCodeRepository.GetByNormalizedCodeAsync(TestCode.ToUpperInvariant())
            .Returns(invitationCode);

        // Act
        var (isValid, errorMessage) = await _service.ValidateAsync(TestCode);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("no longer valid");
    }

    [Fact]
    public async Task ValidateAsync_WithEnvVarCode_ReturnsValid()
    {
        // Arrange
        _invitationCodeRepository.GetByNormalizedCodeAsync(Arg.Any<string>())
            .Returns((InvitationCode?)null);

        // Act
        var (isValid, errorMessage) = await _service.ValidateAsync("ENV-CODE1");

        // Assert
        isValid.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidCode_ReturnsInvalid()
    {
        // Arrange
        _invitationCodeRepository.GetByNormalizedCodeAsync(Arg.Any<string>())
            .Returns((InvitationCode?)null);

        // Act
        var (isValid, errorMessage) = await _service.ValidateAsync("TOTALLY-INVALID");

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().NotBeNullOrEmpty();
    }

    // --- ClaimAsync Tests ---

    [Fact]
    public async Task ClaimAsync_WithDbCode_IncrementsUseCount()
    {
        // Arrange
        var invitationCode = CreateActiveInvitationCode();
        invitationCode.MaxUses = 5;
        invitationCode.UseCount = 0;
        _invitationCodeRepository.GetByNormalizedCodeAsync(TestCode.ToUpperInvariant())
            .Returns(invitationCode);

        // Act
        var result = await _service.ClaimAsync(TestCode, TestUserId);

        // Assert
        result.Should().BeTrue();
        invitationCode.UseCount.Should().Be(1);
        invitationCode.ClaimedByUserId.Should().Be(TestUserId);
        invitationCode.ClaimedAt.Should().NotBeNull();
        await _invitationCodeRepository.Received(1).UpdateAsync(invitationCode);
    }

    [Fact]
    public async Task ClaimAsync_WithMaxUsesReached_SetsStatusToClaimed()
    {
        // Arrange
        var invitationCode = CreateActiveInvitationCode();
        invitationCode.MaxUses = 1;
        invitationCode.UseCount = 0;
        _invitationCodeRepository.GetByNormalizedCodeAsync(TestCode.ToUpperInvariant())
            .Returns(invitationCode);

        // Act
        var result = await _service.ClaimAsync(TestCode, TestUserId);

        // Assert
        result.Should().BeTrue();
        invitationCode.UseCount.Should().Be(1);
        invitationCode.Status.Should().Be(InvitationCodeStatus.Claimed);
        await _invitationCodeRepository.Received(1).UpdateAsync(invitationCode);
    }

    [Fact]
    public async Task ClaimAsync_WithLinkedWaitlistEntry_UpdatesEntryToRegistered()
    {
        // Arrange
        var waitlistEntryId = Guid.NewGuid();
        var waitlistEntry = new WaitlistEntry
        {
            Id = waitlistEntryId,
            Email = "test@example.com",
            NormalizedEmail = "TEST@EXAMPLE.COM",
            Name = "Test User",
            Status = WaitlistStatus.Invited
        };

        var invitationCode = CreateActiveInvitationCode();
        invitationCode.WaitlistEntryId = waitlistEntryId;
        _invitationCodeRepository.GetByNormalizedCodeAsync(TestCode.ToUpperInvariant())
            .Returns(invitationCode);
        _waitlistRepository.GetByIdAsync(waitlistEntryId).Returns(waitlistEntry);

        // Act
        var result = await _service.ClaimAsync(TestCode, TestUserId);

        // Assert
        result.Should().BeTrue();
        waitlistEntry.Status.Should().Be(WaitlistStatus.Registered);
        waitlistEntry.RegisteredAt.Should().NotBeNull();
        await _waitlistRepository.Received(1).UpdateAsync(waitlistEntry);
    }

    [Fact]
    public async Task ClaimAsync_WithEnvVarCode_ReturnsFalse()
    {
        // Arrange
        _invitationCodeRepository.GetByNormalizedCodeAsync(Arg.Any<string>())
            .Returns((InvitationCode?)null);

        // Act
        var result = await _service.ClaimAsync("ENV-CODE1", TestUserId);

        // Assert
        result.Should().BeFalse();
        await _invitationCodeRepository.DidNotReceive().UpdateAsync(Arg.Any<InvitationCode>());
    }

    private static InvitationCode CreateActiveInvitationCode()
    {
        return new InvitationCode
        {
            Id = Guid.NewGuid(),
            Code = TestCode,
            NormalizedCode = TestCode.ToUpperInvariant(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = InvitationCodeStatus.Active,
            MaxUses = 1,
            UseCount = 0
        };
    }
}
