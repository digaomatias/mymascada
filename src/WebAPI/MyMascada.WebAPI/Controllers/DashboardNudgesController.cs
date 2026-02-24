using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.DashboardNudges.Commands;
using MyMascada.Application.Features.DashboardNudges.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/dashboard-nudges")]
[Authorize]
public class DashboardNudgesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public DashboardNudgesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet("dismissed")]
    public async Task<ActionResult<IEnumerable<string>>> GetDismissedNudges()
    {
        try
        {
            var query = new GetDismissedNudgesQuery
            {
                UserId = _currentUserService.GetUserId()
            };

            var dismissed = await _mediator.Send(query);
            return Ok(dismissed);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving dismissed nudges." });
        }
    }

    [HttpPost("{nudgeType}/dismiss")]
    public async Task<ActionResult> DismissNudge(string nudgeType, [FromBody] DismissNudgeRequest? request = null)
    {
        try
        {
            var command = new DismissNudgeCommand
            {
                UserId = _currentUserService.GetUserId(),
                NudgeType = nudgeType,
                SnoozeDays = request?.SnoozeDays ?? 7
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while dismissing the nudge." });
        }
    }
}

public class DismissNudgeRequest
{
    public int? SnoozeDays { get; set; }
}
