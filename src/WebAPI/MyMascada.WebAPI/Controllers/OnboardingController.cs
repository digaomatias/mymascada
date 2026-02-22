using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Onboarding.Commands;
using MyMascada.Application.Features.Onboarding.DTOs;
using MyMascada.Application.Features.Onboarding.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public OnboardingController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpPost("complete")]
    public async Task<ActionResult<OnboardingCompleteResponse>> CompleteOnboarding([FromBody] CompleteOnboardingRequest request)
    {
        try
        {
            var command = new CompleteOnboardingCommand
            {
                UserId = _currentUserService.GetUserId(),
                MonthlyIncome = request.MonthlyIncome,
                MonthlyExpenses = request.MonthlyExpenses,
                GoalName = request.GoalName,
                GoalTargetAmount = request.GoalTargetAmount,
                GoalType = request.GoalType,
                DataEntryMethod = request.DataEntryMethod,
                LinkedAccountId = request.LinkedAccountId
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "An error occurred while completing onboarding." });
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<OnboardingStatusResponse>> GetOnboardingStatus()
    {
        try
        {
            var query = new GetOnboardingStatusQuery
            {
                UserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving onboarding status." });
        }
    }
}
