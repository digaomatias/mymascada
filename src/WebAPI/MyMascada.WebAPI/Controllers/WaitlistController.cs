using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyMascada.Application.Features.Waitlist.Commands;
using MyMascada.Application.Features.Waitlist.DTOs;
using MyMascada.WebAPI.Extensions;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WaitlistController : ControllerBase
{
    private readonly IMediator _mediator;

    public WaitlistController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("signup")]
    [EnableRateLimiting(RateLimitingServiceExtensions.Policies.Authentication)]
    public async Task<ActionResult<JoinWaitlistResponse>> Signup([FromBody] WaitlistSignupRequest request)
    {
        var command = new JoinWaitlistCommand
        {
            Email = request.Email,
            Name = request.Name,
            Locale = request.Locale ?? "en-US",
            IpAddress = GetClientIpAddress()
        };

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return Ok(result);

        return BadRequest(result);
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

public class WaitlistSignupRequest
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Locale { get; set; }
}
