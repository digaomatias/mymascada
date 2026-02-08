using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AccountSharing.Commands;
using MyMascada.Application.Features.AccountSharing.DTOs;
using MyMascada.Application.Features.AccountSharing.Queries;
using MyMascada.Domain.Enums;
using MyMascada.WebAPI.Controllers;

namespace MyMascada.Tests.Unit.Controllers;

public class AccountSharingControllerTests
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFeatureFlags _featureFlags;
    private readonly AccountSharingController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public AccountSharingControllerTests()
    {
        _mediator = Substitute.For<ISender>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetUserId().Returns(_userId);
        _featureFlags = Substitute.For<IFeatureFlags>();

        _controller = new AccountSharingController(_mediator, _currentUserService, _featureFlags);

        SetupUserClaims();
    }

    private void SetupUserClaims()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _userId.ToString())
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    // --- GetAccountShares ---

    [Fact]
    public async Task GetAccountShares_FeatureFlagOff_ReturnsNotFound()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        // Act
        var result = await _controller.GetAccountShares(accountId: 1);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetAccountShares_FeatureFlagOn_ReturnsShares()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        var expectedShares = new List<AccountShareDto>
        {
            new()
            {
                Id = 1,
                AccountId = 1,
                AccountName = "Checking",
                SharedWithUserId = Guid.NewGuid(),
                SharedWithUserEmail = "shared@example.com",
                SharedWithUserName = "Shared User",
                Role = AccountShareRole.Viewer,
                Status = AccountShareStatus.Accepted,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mediator.Send(Arg.Any<GetAccountSharesQuery>())
            .Returns(expectedShares);

        // Act
        var result = await _controller.GetAccountShares(accountId: 1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var shares = okResult.Value.Should().BeAssignableTo<List<AccountShareDto>>().Subject;
        shares.Should().HaveCount(1);
        shares[0].SharedWithUserEmail.Should().Be("shared@example.com");

        await _mediator.Received(1).Send(Arg.Is<GetAccountSharesQuery>(q =>
            q.UserId == _userId && q.AccountId == 1));
    }

    // --- CreateShare ---

    [Fact]
    public async Task CreateShare_FeatureFlagOff_ReturnsNotFound()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        var request = new CreateAccountShareRequest
        {
            Email = "user@example.com",
            Role = AccountShareRole.Viewer
        };

        // Act
        var result = await _controller.CreateShare(accountId: 1, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateShare_ValidRequest_ReturnsCreated()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        var request = new CreateAccountShareRequest
        {
            Email = "friend@example.com",
            Role = AccountShareRole.Manager
        };

        var expectedResult = new CreateAccountShareResult
        {
            Id = 42,
            Token = "test-token-abc123"
        };

        _mediator.Send(Arg.Any<CreateAccountShareCommand>())
            .Returns(expectedResult);

        // Act
        var result = await _controller.CreateShare(accountId: 5, request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(AccountSharingController.GetAccountShares));
        createdResult.RouteValues!["accountId"].Should().Be(5);

        var shareResult = createdResult.Value.Should().BeOfType<CreateAccountShareResult>().Subject;
        shareResult.Id.Should().Be(42);
        shareResult.Token.Should().Be("test-token-abc123");

        await _mediator.Received(1).Send(Arg.Is<CreateAccountShareCommand>(c =>
            c.UserId == _userId &&
            c.AccountId == 5 &&
            c.Email == "friend@example.com" &&
            c.Role == AccountShareRole.Manager));
    }

    // --- RevokeShare ---

    [Fact]
    public async Task RevokeShare_FeatureFlagOff_ReturnsNotFound()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        // Act
        var result = await _controller.RevokeShare(accountId: 1, shareId: 1);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RevokeShare_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        _mediator.Send(Arg.Any<RevokeAccountShareCommand>())
            .Returns(MediatR.Unit.Value);

        // Act
        var result = await _controller.RevokeShare(accountId: 1, shareId: 5);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        await _mediator.Received(1).Send(Arg.Is<RevokeAccountShareCommand>(c =>
            c.UserId == _userId &&
            c.AccountId == 1 &&
            c.ShareId == 5));
    }

    // --- AcceptShare ---

    [Fact]
    public async Task AcceptShare_FeatureFlagOff_ReturnsNotFound()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        var request = new AcceptDeclineShareRequest { Token = "some-token" };

        // Act
        var result = await _controller.AcceptShare(request);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AcceptShare_ValidToken_ReturnsOk()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        var request = new AcceptDeclineShareRequest { Token = "valid-token" };

        var expectedDto = new AccountShareDto
        {
            Id = 10,
            AccountId = 3,
            AccountName = "Shared Checking",
            SharedWithUserId = _userId,
            SharedWithUserEmail = "me@example.com",
            SharedWithUserName = "Test User",
            Role = AccountShareRole.Viewer,
            Status = AccountShareStatus.Accepted,
            CreatedAt = DateTime.UtcNow
        };

        _mediator.Send(Arg.Any<AcceptAccountShareCommand>())
            .Returns(expectedDto);

        // Act
        var result = await _controller.AcceptShare(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var shareDto = okResult.Value.Should().BeOfType<AccountShareDto>().Subject;
        shareDto.Id.Should().Be(10);
        shareDto.AccountName.Should().Be("Shared Checking");
        shareDto.Status.Should().Be(AccountShareStatus.Accepted);

        await _mediator.Received(1).Send(Arg.Is<AcceptAccountShareCommand>(c =>
            c.Token == "valid-token" &&
            c.UserId == _userId));
    }

    // --- DeclineShare ---

    [Fact]
    public async Task DeclineShare_FeatureFlagOff_ReturnsNotFound()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        var request = new AcceptDeclineShareRequest { Token = "some-token" };

        // Act
        var result = await _controller.DeclineShare(request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeclineShare_ValidToken_ReturnsNoContent()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        var request = new AcceptDeclineShareRequest { Token = "decline-token" };

        _mediator.Send(Arg.Any<DeclineAccountShareCommand>())
            .Returns(MediatR.Unit.Value);

        // Act
        var result = await _controller.DeclineShare(request);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        await _mediator.Received(1).Send(Arg.Is<DeclineAccountShareCommand>(c =>
            c.Token == "decline-token" &&
            c.UserId == _userId));
    }

    // --- UpdateShareRole ---

    [Fact]
    public async Task UpdateShareRole_FeatureFlagOff_ReturnsNotFound()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        var request = new UpdateShareRoleRequest { Role = AccountShareRole.Manager };

        // Act
        var result = await _controller.UpdateShareRole(accountId: 1, shareId: 1, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateShareRole_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        var request = new UpdateShareRoleRequest { Role = AccountShareRole.Manager };

        _mediator.Send(Arg.Any<UpdateAccountShareRoleCommand>())
            .Returns(MediatR.Unit.Value);

        // Act
        var result = await _controller.UpdateShareRole(accountId: 2, shareId: 7, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        await _mediator.Received(1).Send(Arg.Is<UpdateAccountShareRoleCommand>(c =>
            c.UserId == _userId &&
            c.AccountId == 2 &&
            c.ShareId == 7 &&
            c.NewRole == AccountShareRole.Manager));
    }

    // --- GetReceivedShares ---

    [Fact]
    public async Task GetReceivedShares_FeatureFlagOff_ReturnsNotFound()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(false);

        // Act
        var result = await _controller.GetReceivedShares();

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetReceivedShares_FeatureFlagOn_ReturnsReceivedShares()
    {
        // Arrange
        _featureFlags.AccountSharing.Returns(true);

        var expectedShares = new List<ReceivedShareDto>
        {
            new()
            {
                Id = 1,
                AccountId = 10,
                AccountName = "Shared Account",
                SharedByName = "Owner Name",
                Role = AccountShareRole.Viewer,
                Status = AccountShareStatus.Pending,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mediator.Send(Arg.Any<GetReceivedSharesQuery>())
            .Returns(expectedShares);

        // Act
        var result = await _controller.GetReceivedShares();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var shares = okResult.Value.Should().BeAssignableTo<List<ReceivedShareDto>>().Subject;
        shares.Should().HaveCount(1);
        shares[0].AccountName.Should().Be("Shared Account");

        await _mediator.Received(1).Send(Arg.Is<GetReceivedSharesQuery>(q =>
            q.UserId == _userId));
    }
}
