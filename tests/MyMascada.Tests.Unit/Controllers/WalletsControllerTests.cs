using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.Commands;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Application.Features.Wallets.Queries;
using MyMascada.WebAPI.Controllers;

namespace MyMascada.Tests.Unit.Controllers;

public class WalletsControllerTests
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly WalletsController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public WalletsControllerTests()
    {
        _mediator = Substitute.For<IMediator>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.GetUserId().Returns(_userId);

        _controller = new WalletsController(_mediator, _currentUserService);

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

    [Fact]
    public async Task GetWallets_ShouldReturnOkWithWalletList()
    {
        // Arrange
        var wallets = new List<WalletSummaryDto>
        {
            new() { Id = 1, Name = "Vacation", Balance = 500m, Currency = "USD" },
            new() { Id = 2, Name = "Emergency", Balance = 1000m, Currency = "USD" }
        };

        _mediator.Send(Arg.Any<GetWalletsQuery>())
            .Returns(wallets.AsEnumerable());

        // Act
        var result = await _controller.GetWallets();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedWallets = okResult.Value.Should().BeAssignableTo<IEnumerable<WalletSummaryDto>>().Subject;
        returnedWallets.Should().HaveCount(2);

        await _mediator.Received(1).Send(Arg.Is<GetWalletsQuery>(q =>
            q.UserId == _userId && q.IncludeArchived == false));
    }

    [Fact]
    public async Task GetWallet_WithExistingId_ShouldReturnOk()
    {
        // Arrange
        var walletDetail = new WalletDetailDto
        {
            Id = 1,
            Name = "Vacation Fund",
            Balance = 500m,
            Currency = "USD",
            Allocations = new List<WalletAllocationDto>()
        };

        _mediator.Send(Arg.Any<GetWalletQuery>())
            .Returns(walletDetail);

        // Act
        var result = await _controller.GetWallet(1);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var wallet = okResult.Value.Should().BeOfType<WalletDetailDto>().Subject;
        wallet.Id.Should().Be(1);
        wallet.Name.Should().Be("Vacation Fund");

        await _mediator.Received(1).Send(Arg.Is<GetWalletQuery>(q =>
            q.WalletId == 1 && q.UserId == _userId));
    }

    [Fact]
    public async Task GetWallet_WhenNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _mediator.Send(Arg.Any<GetWalletQuery>())
            .Returns((WalletDetailDto?)null);

        // Act
        var result = await _controller.GetWallet(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateWallet_WithValidRequest_ShouldReturnCreatedAtAction()
    {
        // Arrange
        var request = new CreateWalletRequest
        {
            Name = "New Wallet",
            Icon = "star",
            Color = "#00FF00",
            Currency = "EUR"
        };

        var createdWallet = new WalletDetailDto
        {
            Id = 5,
            Name = "New Wallet",
            Icon = "star",
            Color = "#00FF00",
            Currency = "EUR",
            Allocations = new List<WalletAllocationDto>()
        };

        _mediator.Send(Arg.Any<CreateWalletCommand>())
            .Returns(createdWallet);

        // Act
        var result = await _controller.CreateWallet(request);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.ActionName.Should().Be(nameof(WalletsController.GetWallet));
        createdResult.RouteValues!["id"].Should().Be(5);

        var wallet = createdResult.Value.Should().BeOfType<WalletDetailDto>().Subject;
        wallet.Name.Should().Be("New Wallet");

        await _mediator.Received(1).Send(Arg.Is<CreateWalletCommand>(c =>
            c.Name == "New Wallet" &&
            c.Currency == "EUR" &&
            c.UserId == _userId));
    }

    [Fact]
    public async Task UpdateWallet_WithValidRequest_ShouldReturnOk()
    {
        // Arrange
        var request = new UpdateWalletRequest
        {
            Name = "Updated Name",
            Currency = "GBP"
        };

        var updatedWallet = new WalletDetailDto
        {
            Id = 1,
            Name = "Updated Name",
            Currency = "GBP",
            Allocations = new List<WalletAllocationDto>()
        };

        _mediator.Send(Arg.Any<UpdateWalletCommand>())
            .Returns(updatedWallet);

        // Act
        var result = await _controller.UpdateWallet(1, request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var wallet = okResult.Value.Should().BeOfType<WalletDetailDto>().Subject;
        wallet.Name.Should().Be("Updated Name");
        wallet.Currency.Should().Be("GBP");

        await _mediator.Received(1).Send(Arg.Is<UpdateWalletCommand>(c =>
            c.WalletId == 1 &&
            c.Name == "Updated Name" &&
            c.Currency == "GBP" &&
            c.UserId == _userId));
    }

    [Fact]
    public async Task DeleteWallet_ShouldReturnNoContent()
    {
        // Arrange
        _mediator.Send(Arg.Any<DeleteWalletCommand>())
            .Returns(MediatR.Unit.Value);

        // Act
        var result = await _controller.DeleteWallet(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        await _mediator.Received(1).Send(Arg.Is<DeleteWalletCommand>(c =>
            c.WalletId == 1 && c.UserId == _userId));
    }

    [Fact]
    public async Task CreateAllocation_WithValidRequest_ShouldReturnStatusCode201()
    {
        // Arrange
        var request = new CreateAllocationRequest
        {
            TransactionId = 10,
            Amount = 75.50m,
            Note = "Partial allocation"
        };

        var createdAllocation = new WalletAllocationDto
        {
            Id = 50,
            TransactionId = 10,
            TransactionDescription = "Groceries",
            Amount = 75.50m,
            Note = "Partial allocation",
            CreatedAt = DateTime.UtcNow
        };

        _mediator.Send(Arg.Any<CreateWalletAllocationCommand>())
            .Returns(createdAllocation);

        // Act
        var result = await _controller.CreateAllocation(1, request);

        // Assert
        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);

        var allocation = objectResult.Value.Should().BeOfType<WalletAllocationDto>().Subject;
        allocation.Id.Should().Be(50);
        allocation.Amount.Should().Be(75.50m);

        await _mediator.Received(1).Send(Arg.Is<CreateWalletAllocationCommand>(c =>
            c.WalletId == 1 &&
            c.TransactionId == 10 &&
            c.Amount == 75.50m &&
            c.UserId == _userId));
    }

    [Fact]
    public async Task DeleteAllocation_ShouldReturnNoContent()
    {
        // Arrange
        _mediator.Send(Arg.Any<DeleteWalletAllocationCommand>())
            .Returns(MediatR.Unit.Value);

        // Act
        var result = await _controller.DeleteAllocation(1, 50);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        await _mediator.Received(1).Send(Arg.Is<DeleteWalletAllocationCommand>(c =>
            c.WalletId == 1 && c.AllocationId == 50 && c.UserId == _userId));
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnOk()
    {
        // Arrange
        var dashboard = new WalletDashboardSummaryDto
        {
            TotalBalance = 1500m,
            Wallets = new List<WalletSummaryDto>
            {
                new() { Id = 1, Name = "Vacation", Balance = 500m },
                new() { Id = 2, Name = "Emergency", Balance = 1000m }
            }
        };

        _mediator.Send(Arg.Any<GetWalletDashboardQuery>())
            .Returns(dashboard);

        // Act
        var result = await _controller.GetDashboard();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedDashboard = okResult.Value.Should().BeOfType<WalletDashboardSummaryDto>().Subject;
        returnedDashboard.TotalBalance.Should().Be(1500m);
        returnedDashboard.Wallets.Should().HaveCount(2);

        await _mediator.Received(1).Send(Arg.Is<GetWalletDashboardQuery>(q =>
            q.UserId == _userId));
    }

    [Fact]
    public async Task CreateWallet_WhenUnauthorizedAccessException_ShouldReturnUnauthorized()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.GetUserId().Returns(_ => throw new UnauthorizedAccessException("Not authenticated"));

        var controller = new WalletsController(_mediator, currentUserService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var request = new CreateWalletRequest
        {
            Name = "Test",
            Currency = "USD"
        };

        // Act
        var result = await controller.CreateWallet(request);

        // Assert
        var unauthorizedResult = result.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task CreateWallet_WhenArgumentException_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new CreateWalletRequest
        {
            Name = "Duplicate",
            Currency = "USD"
        };

        _mediator.Send(Arg.Any<CreateWalletCommand>())
            .Returns<WalletDetailDto>(_ => throw new ArgumentException("A wallet with the name 'Duplicate' already exists."));

        // Act
        var result = await _controller.CreateWallet(request);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }
}
