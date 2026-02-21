using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Goals.Commands;
using MyMascada.Application.Features.Goals.DTOs;
using MyMascada.Application.Features.Goals.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GoalsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public GoalsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all goals for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<GoalSummaryDto>>> GetGoals(
        [FromQuery] bool includeCompleted = false)
    {
        try
        {
            var query = new GetGoalsQuery
            {
                UserId = _currentUserService.GetUserId(),
                IncludeCompleted = includeCompleted
            };

            var goals = await _mediator.Send(query);
            return Ok(goals);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving goals." });
        }
    }

    /// <summary>
    /// Get a specific goal by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<GoalDetailDto>> GetGoal(int id)
    {
        try
        {
            var query = new GetGoalQuery
            {
                GoalId = id,
                UserId = _currentUserService.GetUserId()
            };

            var goal = await _mediator.Send(query);

            if (goal == null)
            {
                return NotFound(new { message = "Goal not found or you don't have permission to access it." });
            }

            return Ok(goal);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the goal." });
        }
    }

    /// <summary>
    /// Create a new goal
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<GoalDetailDto>> CreateGoal([FromBody] CreateGoalRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new CreateGoalCommand
            {
                Name = request.Name,
                Description = request.Description,
                TargetAmount = request.TargetAmount,
                Deadline = request.Deadline,
                GoalType = request.GoalType,
                LinkedAccountId = request.LinkedAccountId,
                UserId = _currentUserService.GetUserId()
            };

            var goal = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetGoal), new { id = goal.Id }, goal);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the goal." });
        }
    }

    /// <summary>
    /// Update an existing goal
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<GoalDetailDto>> UpdateGoal(int id, [FromBody] UpdateGoalRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new UpdateGoalCommand
            {
                GoalId = id,
                Name = request.Name,
                Description = request.Description,
                TargetAmount = request.TargetAmount,
                CurrentAmount = request.CurrentAmount,
                Status = request.Status,
                Deadline = request.Deadline,
                LinkedAccountId = request.LinkedAccountId,
                UserId = _currentUserService.GetUserId()
            };

            var goal = await _mediator.Send(command);
            return Ok(goal);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the goal." });
        }
    }

    /// <summary>
    /// Delete a goal (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteGoal(int id)
    {
        try
        {
            var command = new DeleteGoalCommand
            {
                GoalId = id,
                UserId = _currentUserService.GetUserId()
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the goal." });
        }
    }

    /// <summary>
    /// Update goal progress (convenience endpoint for updating CurrentAmount only)
    /// </summary>
    [HttpPut("{id}/progress")]
    public async Task<ActionResult<GoalDetailDto>> UpdateGoalProgress(int id, [FromBody] UpdateGoalProgressRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new UpdateGoalCommand
            {
                GoalId = id,
                CurrentAmount = request.CurrentAmount,
                UserId = _currentUserService.GetUserId()
            };

            var goal = await _mediator.Send(command);
            return Ok(goal);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating goal progress." });
        }
    }
}
