using MyMascada.Domain.Enums;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;
using MyMascada.Application.Features.Waitlist.Commands;
using MyMascada.Application.Features.Waitlist.DTOs;
using MyMascada.Domain.Entities;
using NSubstitute.ExceptionExtensions;

namespace MyMascada.Tests.Unit.Features.Waitlist.Commands;

public class JoinWaitlistCommandHandlerTests
{
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IValidator<JoinWaitlistCommand> _validator;
    private readonly ILogger<JoinWaitlistCommandHandler> _logger;
    private readonly JoinWaitlistCommandHandler _handler;

    private const string TestEmail = "test@example.com";
    private const string TestName = "Test User";

    public JoinWaitlistCommandHandlerTests()
    {
        _waitlistRepository = Substitute.For<IWaitlistRepository>();
        _userRepository = Substitute.For<IUserRepository>();
        _emailService = Substitute.For<IEmailService>();
        _emailTemplateService = Substitute.For<IEmailTemplateService>();
        _validator = Substitute.For<IValidator<JoinWaitlistCommand>>();
        _logger = Substitute.For<ILogger<JoinWaitlistCommandHandler>>();

        _handler = new JoinWaitlistCommandHandler(
            _waitlistRepository,
            _userRepository,
            _emailService,
            _emailTemplateService,
            _validator,
            _logger);
    }

    [Fact]
    public async Task Handle_WithValidInput_CreatesEntryAndSendsEmail()
    {
        // Arrange
        SetupValidRequest();
        _waitlistRepository.GetByNormalizedEmailAsync(Arg.Any<string>()).Returns((WaitlistEntry?)null);
        _userRepository.ExistsByEmailAsync(TestEmail).Returns(false);
        _emailTemplateService.RenderAsync("waitlist-confirmation", Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(("Welcome!", "<h1>Welcome</h1>"));
        _emailService.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().NotBeNullOrEmpty();
        await _waitlistRepository.Received(1).AddAsync(Arg.Is<WaitlistEntry>(e =>
            e.Email == TestEmail &&
            e.Name == TestName &&
            e.Status == WaitlistStatus.Pending));
        await _emailService.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == TestEmail),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ReturnsSilentSuccess()
    {
        // Arrange
        SetupValidRequest();
        var existingEntry = new WaitlistEntry
        {
            Id = Guid.NewGuid(),
            Email = TestEmail,
            NormalizedEmail = TestEmail.ToUpperInvariant(),
            Name = TestName,
            Status = WaitlistStatus.Pending
        };
        _waitlistRepository.GetByNormalizedEmailAsync(TestEmail.ToUpperInvariant()).Returns(existingEntry);

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _waitlistRepository.DidNotReceive().AddAsync(Arg.Any<WaitlistEntry>());
        await _emailService.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithExistingAccount_ReturnsAlreadyRegistered()
    {
        // Arrange
        SetupValidRequest();
        _waitlistRepository.GetByNormalizedEmailAsync(Arg.Any<string>()).Returns((WaitlistEntry?)null);
        _userRepository.ExistsByEmailAsync(TestEmail).Returns(true);

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("You already have an account!");
        await _waitlistRepository.DidNotReceive().AddAsync(Arg.Any<WaitlistEntry>());
    }

    [Fact]
    public async Task Handle_WithInvalidInput_ReturnsValidationErrors()
    {
        // Arrange
        _validator.ValidateAsync(Arg.Any<JoinWaitlistCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("Email", "Email is required"),
                new ValidationFailure("Name", "Name is required")
            }));

        var command = new JoinWaitlistCommand { Email = "", Name = "" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Email is required");
        result.Errors.Should().Contain("Name is required");
        await _waitlistRepository.DidNotReceive().AddAsync(Arg.Any<WaitlistEntry>());
    }

    [Fact]
    public async Task Handle_WhenEmailFails_StillReturnsSuccess()
    {
        // Arrange
        SetupValidRequest();
        _waitlistRepository.GetByNormalizedEmailAsync(Arg.Any<string>()).Returns((WaitlistEntry?)null);
        _userRepository.ExistsByEmailAsync(TestEmail).Returns(false);
        _emailTemplateService.RenderAsync("waitlist-confirmation", Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(("Welcome!", "<h1>Welcome</h1>"));
        _emailService.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("SMTP connection failed"));

        var command = CreateValidCommand();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _waitlistRepository.Received(1).AddAsync(Arg.Any<WaitlistEntry>());
    }

    [Fact]
    public async Task Handle_NormalizesEmailToUpperCase()
    {
        // Arrange
        SetupValidRequest();
        _waitlistRepository.GetByNormalizedEmailAsync(Arg.Any<string>()).Returns((WaitlistEntry?)null);
        _userRepository.ExistsByEmailAsync(TestEmail).Returns(false);
        _emailTemplateService.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(("Subject", "Body"));
        _emailService.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-123"));

        var command = CreateValidCommand();

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _waitlistRepository.Received(1).GetByNormalizedEmailAsync(TestEmail.ToUpperInvariant());
    }

    [Fact]
    public async Task Handle_SetsCorrectLocaleForPtBR()
    {
        // Arrange
        SetupValidRequest();
        _waitlistRepository.GetByNormalizedEmailAsync(Arg.Any<string>()).Returns((WaitlistEntry?)null);
        _userRepository.ExistsByEmailAsync(TestEmail).Returns(false);
        _emailTemplateService.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object>>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(("Assunto", "<h1>Bem-vindo</h1>"));
        _emailService.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(EmailResult.Succeeded("msg-456"));

        var command = new JoinWaitlistCommand
        {
            Email = TestEmail,
            Name = TestName,
            Locale = "pt-BR"
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _emailTemplateService.Received(1).RenderAsync(
            "waitlist-confirmation",
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            "pt-BR",
            Arg.Any<CancellationToken>());
    }

    private JoinWaitlistCommand CreateValidCommand()
    {
        return new JoinWaitlistCommand
        {
            Email = TestEmail,
            Name = TestName,
            Locale = "en-US"
        };
    }

    private void SetupValidRequest()
    {
        _validator.ValidateAsync(Arg.Any<JoinWaitlistCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
    }
}
