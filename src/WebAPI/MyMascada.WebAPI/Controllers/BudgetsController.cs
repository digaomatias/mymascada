using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.Commands;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BudgetsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public BudgetsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all budgets for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BudgetSummaryDto>>> GetBudgets(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool onlyCurrentPeriod = false)
    {
        try
        {
            var query = new GetBudgetsQuery
            {
                UserId = _currentUserService.GetUserId(),
                IncludeInactive = includeInactive,
                OnlyCurrentPeriod = onlyCurrentPeriod
            };

            var budgets = await _mediator.Send(query);
            return Ok(budgets);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving budgets." });
        }
    }

    /// <summary>
    /// Get a specific budget by ID with full progress details
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<BudgetDetailDto>> GetBudget(int id)
    {
        try
        {
            var query = new GetBudgetQuery
            {
                BudgetId = id,
                UserId = _currentUserService.GetUserId()
            };

            var budget = await _mediator.Send(query);

            if (budget == null)
            {
                return NotFound(new { message = "Budget not found or you don't have permission to access it." });
            }

            return Ok(budget);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the budget." });
        }
    }

    /// <summary>
    /// Get budget suggestions based on historical spending
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<IEnumerable<BudgetSuggestionDto>>> GetBudgetSuggestions(
        [FromQuery] int monthsToAnalyze = 3)
    {
        try
        {
            var query = new GetBudgetSuggestionsQuery
            {
                UserId = _currentUserService.GetUserId(),
                MonthsToAnalyze = Math.Clamp(monthsToAnalyze, 1, 12)
            };

            var suggestions = await _mediator.Send(query);
            return Ok(suggestions);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating budget suggestions." });
        }
    }

    /// <summary>
    /// Create a new budget
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BudgetDetailDto>> CreateBudget([FromBody] CreateBudgetRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new CreateBudgetCommand
            {
                Name = request.Name,
                Description = request.Description,
                PeriodType = request.PeriodType,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsRecurring = request.IsRecurring,
                Categories = request.Categories,
                UserId = _currentUserService.GetUserId()
            };

            var budget = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetBudget), new { id = budget.Id }, budget);
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
            return StatusCode(500, new { message = "An error occurred while creating the budget." });
        }
    }

    /// <summary>
    /// Update an existing budget
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<BudgetDetailDto>> UpdateBudget(int id, [FromBody] UpdateBudgetRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new UpdateBudgetCommand
            {
                BudgetId = id,
                Name = request.Name,
                Description = request.Description,
                IsActive = request.IsActive,
                IsRecurring = request.IsRecurring,
                UserId = _currentUserService.GetUserId()
            };

            var budget = await _mediator.Send(command);
            return Ok(budget);
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
            return StatusCode(500, new { message = "An error occurred while updating the budget." });
        }
    }

    /// <summary>
    /// Delete a budget (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteBudget(int id)
    {
        try
        {
            var command = new DeleteBudgetCommand
            {
                BudgetId = id,
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
            return StatusCode(500, new { message = "An error occurred while deleting the budget." });
        }
    }

    /// <summary>
    /// Add a category allocation to an existing budget
    /// </summary>
    [HttpPost("{id}/categories")]
    public async Task<ActionResult<BudgetDetailDto>> AddBudgetCategory(
        int id,
        [FromBody] CreateBudgetCategoryRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new AddBudgetCategoryCommand
            {
                BudgetId = id,
                CategoryId = request.CategoryId,
                BudgetedAmount = request.BudgetedAmount,
                AllowRollover = request.AllowRollover,
                CarryOverspend = request.CarryOverspend,
                IncludeSubcategories = request.IncludeSubcategories,
                Notes = request.Notes,
                UserId = _currentUserService.GetUserId()
            };

            var budget = await _mediator.Send(command);
            return Ok(budget);
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
            return StatusCode(500, new { message = "An error occurred while adding the category." });
        }
    }

    /// <summary>
    /// Update a category allocation in a budget
    /// </summary>
    [HttpPut("{id}/categories/{categoryId}")]
    public async Task<ActionResult<BudgetDetailDto>> UpdateBudgetCategory(
        int id,
        int categoryId,
        [FromBody] UpdateBudgetCategoryRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new UpdateBudgetCategoryCommand
            {
                BudgetId = id,
                CategoryId = categoryId,
                BudgetedAmount = request.BudgetedAmount,
                AllowRollover = request.AllowRollover,
                CarryOverspend = request.CarryOverspend,
                IncludeSubcategories = request.IncludeSubcategories,
                Notes = request.Notes,
                UserId = _currentUserService.GetUserId()
            };

            var budget = await _mediator.Send(command);
            return Ok(budget);
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
            return StatusCode(500, new { message = "An error occurred while updating the category." });
        }
    }

    /// <summary>
    /// Remove a category allocation from a budget
    /// </summary>
    [HttpDelete("{id}/categories/{categoryId}")]
    public async Task<ActionResult<BudgetDetailDto>> RemoveBudgetCategory(int id, int categoryId)
    {
        try
        {
            var command = new RemoveBudgetCategoryCommand
            {
                BudgetId = id,
                CategoryId = categoryId,
                UserId = _currentUserService.GetUserId()
            };

            var budget = await _mediator.Send(command);
            return Ok(budget);
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
            return StatusCode(500, new { message = "An error occurred while removing the category." });
        }
    }

    /// <summary>
    /// Process budget rollovers for ended periods.
    /// Creates next period budgets for recurring budgets with rollover-enabled categories.
    /// </summary>
    /// <param name="previewOnly">If true, only shows what would be rolled over without making changes</param>
    [HttpPost("process-rollovers")]
    public async Task<ActionResult<BudgetRolloverResultDto>> ProcessRollovers([FromQuery] bool previewOnly = false)
    {
        try
        {
            var command = new ProcessBudgetRolloversCommand
            {
                UserId = _currentUserService.GetUserId(),
                PreviewOnly = previewOnly
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing budget rollovers." });
        }
    }
}
