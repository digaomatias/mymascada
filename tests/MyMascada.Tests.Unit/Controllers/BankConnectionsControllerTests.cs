using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Queries;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;
using MyMascada.WebAPI.Controllers;

namespace MyMascada.Tests.Unit.Controllers;

public class BankConnectionsControllerTests
{
    [Fact]
    public async Task ExchangeAkahuCode_AlwaysUsesConfiguredAppIdToken()
    {
        var mediator = Substitute.For<IMediator>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userId = Guid.NewGuid();
        currentUserService.GetUserId().Returns(userId);

        var options = Options.Create(new AkahuOptions
        {
            AppIdToken = "app_token_from_config"
        });

        mediator.Send(Arg.Any<ExchangeAkahuCodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ExchangeAkahuCodeResult(Array.Empty<MyMascada.Application.Features.BankConnections.DTOs.AkahuAccountDto>(), "access_token"));

        var controller = new BankConnectionsController(mediator, currentUserService, options);

        var response = await controller.ExchangeAkahuCode(new ExchangeAkahuCodeRequest("code-1", null));

        response.Result.Should().BeOfType<OkObjectResult>();

        await mediator.Received(1).Send(
            Arg.Is<ExchangeAkahuCodeQuery>(q =>
                q.UserId == userId &&
                q.Code == "code-1" &&
                q.State == null &&
                q.AppIdToken == "app_token_from_config"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExchangeAkahuCode_ReturnsBadRequest_WhenConfiguredTokenMissing()
    {
        var mediator = Substitute.For<IMediator>();
        var currentUserService = Substitute.For<ICurrentUserService>();
        var options = Options.Create(new AkahuOptions());

        var controller = new BankConnectionsController(mediator, currentUserService, options);

        var response = await controller.ExchangeAkahuCode(new ExchangeAkahuCodeRequest("code-1", null));

        response.Result.Should().BeOfType<BadRequestObjectResult>();
        await mediator.DidNotReceive().Send(Arg.Any<ExchangeAkahuCodeQuery>(), Arg.Any<CancellationToken>());
    }
}
