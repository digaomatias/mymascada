using MyMascada.Domain.Enums;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;
using MyMascada.Application.Features.Waitlist.Commands;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Features.Waitlist.Commands;

public class GenerateInvitationCommandHandlerTests
{
    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly IEmailServiceFactory _emailServiceFactory;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<GenerateInvitationCommandHandler> _logger;
    private readonly GenerateInvitationCommandHandler _handler;

    private const string TestEmail = "test@example.com";
    private const string TestName = "Test User";
    private const string UnambiguousChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public GenerateInvitationCommandHandlerTests()
    {
        _invitationCodeRepository = Substitute.For<IInvitationCodeRepository>();
        _waitlistRepository = Substitute.For<IWaitlistRepository>();
        _emailServiceFactory = Substitute.For<IEmailServiceFactory>();
        _emailService = Substitute.For<IEmailService>();
        _emailServiceFactory.GetDefaultProvider().Returns(_emailService);
        _emailTemplateService = Substitute.For<IEmailTemplateService>();
        _logger = Substitute.For<ILogger<GenerateInvitationCommandHandler>>();

        _handler = new GenerateInvitationCommandHandler(
            _invitationCodeRepository,
            _waitlistRepository,
            _emailServiceFactory,
            _emailTemplateService,
            _logger);
    }

    [Fact]
    public async Task Handle_WithoutWaitlistEntry_GeneratesCodeSuccessfully()
    {
        // Arrange
        SetupEmailMocks();
        var command = new GenerateInvitationCommand
        {
            Email = TestEmail,
            ExpiresInDays = 7,
            MaxUses = 1,
            SendEmail = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Code.Should().NotBeNullOrEmpty();
        result.Code.Should().StartWith("MYMASC-");
        result.InvitationCodeId.Should().NotBeNull();
        result.ExpiresAt.Should().NotBeNull();
        await _invitationCodeRepository.Received(1).AddAsync(Arg.Any<InvitationCode>());
        await _waitlistRepository.DidNotReceive().UpdateAsync(Arg.Any<WaitlistEntry>());
    }

    [Fact]
    public async Task Handle_WithWaitlistEntry_LinksAndUpdatesStatus()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var waitlistEntry = new WaitlistEntry
        {
            Id = entryId,
            Email = TestEmail,
            NormalizedEmail = TestEmail.ToUpperInvariant(),
            Name = TestName,
            Status = WaitlistStatus.Pending,
            Locale = "en-US"
        };
        _waitlistRepository.GetByIdAsync(entryId).Returns(waitlistEntry);
        SetupEmailMocks();

        var command = new GenerateInvitationCommand
        {
            WaitlistEntryId = entryId,
            ExpiresInDays = 7,
            MaxUses = 1,
            SendEmail = true
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _invitationCodeRepository.Received(1).RevokeActiveCodesForEntryAsync(entryId);
        await _invitationCodeRepository.Received(1).AddAsync(Arg.Is<InvitationCode>(c =>
            c.WaitlistEntryId == entryId));
        waitlistEntry.Status.Should().Be(WaitlistStatus.Invited);
        waitlistEntry.InvitedAt.Should().NotBeNull();
        await _waitlistRepository.Received(1).UpdateAsync(waitlistEntry);
    }

    [Fact]
    public async Task Handle_WithInvalidWaitlistEntry_ReturnsError()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        _waitlistRepository.GetByIdAsync(entryId).Returns((WaitlistEntry?)null);

        var command = new GenerateInvitationCommand
        {
            WaitlistEntryId = entryId,
            ExpiresInDays = 7
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Waitlist entry not found.");
        await _invitationCodeRepository.DidNotReceive().AddAsync(Arg.Any<InvitationCode>());
    }

    [Fact]
    public async Task Handle_GeneratesCodeWithCorrectFormat()
    {
        // Arrange
        SetupEmailMocks();
        var command = new GenerateInvitationCommand
        {
            Email = TestEmail,
            ExpiresInDays = 7,
            MaxUses = 1,
            SendEmail = false
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Code.Should().NotBeNull();
        result.Code.Should().StartWith("MYMASC-");
        result.Code!.Length.Should().Be(13); // "MYMASC-" (7) + 6 chars
        var suffix = result.Code[7..];
        foreach (var c in suffix)
        {
            UnambiguousChars.Should().Contain(c.ToString());
        }
    }

    [Fact]
    public async Task Handle_WhenSendEmailTrue_SendsInvitationEmail()
    {
        // Arrange
        SetupEmailMocks();
        var command = new GenerateInvitationCommand
        {
            Email = TestEmail,
            ExpiresInDays = 7,
            MaxUses = 1,
            SendEmail = true
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailTemplateService.Received(1).RenderAsync(
            "invitation",
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _emailService.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == TestEmail),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSendEmailFalse_DoesNotSendEmail()
    {
        // Arrange
        var command = new GenerateInvitationCommand
        {
            Email = TestEmail,
            ExpiresInDays = 7,
            MaxUses = 1,
            SendEmail = false
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailTemplateService.DidNotReceive().RenderAsync(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _emailService.DidNotReceive().SendAsync(
            Arg.Any<EmailMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SetsExpirationCorrectly()
    {
        // Arrange
        SetupEmailMocks();
        var expiresInDays = 14;
        var beforeRequest = DateTime.UtcNow;
        var command = new GenerateInvitationCommand
        {
            Email = TestEmail,
            ExpiresInDays = expiresInDays,
            MaxUses = 1,
            SendEmail = false
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ExpiresAt.Should().NotBeNull();
        result.ExpiresAt!.Value.Should().BeCloseTo(
            beforeRequest.AddDays(expiresInDays), TimeSpan.FromSeconds(5));
    }

    private void SetupEmailMocks()
    {
        _emailTemplateService.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(("Invitation Subject", "<h1>You are invited</h1>"));
        _emailService.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));
    }
}
