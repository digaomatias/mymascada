using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.Commands;
using MyMascada.Application.Features.RecurringTransactions.DTOs;
using MyMascada.Application.Features.RecurringTransactions.Queries;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// Manages user-created recurring transactions (scheduled bills).
/// Distinct from the heuristic recurring pattern detection.
///
/// API conventions:
/// - All dates are date-only: send "yyyy-MM-dd" strings (timezone-offset ISO
///   strings can shift across midnight and change the monthly anchor day).
/// - Amounts are positive and expense-only; auto-created transactions are
///   recorded with a negative amount. Recurring income is a future direction flag.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/recurring-transactions")]
[Route("api/latest/recurring-transactions")]
[Authorize]
public class RecurringTransactionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public RecurringTransactionsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all recurring transactions for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RecurringTransactionDto>>> GetRecurringTransactions(
        [FromQuery] bool includeInactive = false)
    {
        var query = new GetRecurringTransactionsQuery
        {
            UserId = _currentUserService.GetUserId(),
            IncludeInactive = includeInactive
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get the materialized upcoming schedule for the current user's active recurring transactions
    /// </summary>
    [HttpGet("upcoming")]
    public async Task<ActionResult<List<UpcomingRecurringTransactionDto>>> GetUpcoming(
        [FromQuery] int daysAhead = 30)
    {
        var query = new GetUpcomingRecurringTransactionsQuery
        {
            UserId = _currentUserService.GetUserId(),
            DaysAhead = daysAhead
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get a single recurring transaction with its recent occurrences
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecurringTransactionDetailDto>> GetRecurringTransaction(int id)
    {
        var query = new GetRecurringTransactionQuery
        {
            Id = id,
            UserId = _currentUserService.GetUserId()
        };

        var result = await _mediator.Send(query);
        if (result == null)
        {
            return NotFound(new { message = $"Recurring transaction with ID {id} not found." });
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new recurring transaction
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RecurringTransactionDto>> CreateRecurringTransaction(
        [FromBody] CreateRecurringTransactionRequest request)
    {
        var command = new CreateRecurringTransactionCommand
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            Description = request.Description,
            Amount = request.Amount,
            Frequency = request.Frequency,
            CustomIntervalDays = request.CustomIntervalDays,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            AutoCreate = request.AutoCreate,
            Notes = request.Notes
        };

        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetRecurringTransaction), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing recurring transaction
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecurringTransactionDto>> UpdateRecurringTransaction(
        int id,
        [FromBody] UpdateRecurringTransactionRequest request)
    {
        var command = new UpdateRecurringTransactionCommand
        {
            Id = id,
            UserId = _currentUserService.GetUserId(),
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            Description = request.Description,
            Amount = request.Amount,
            Frequency = request.Frequency,
            CustomIntervalDays = request.CustomIntervalDays,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            AutoCreate = request.AutoCreate,
            Notes = request.Notes
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Soft delete a recurring transaction
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteRecurringTransaction(int id)
    {
        var command = new DeleteRecurringTransactionCommand
        {
            Id = id,
            UserId = _currentUserService.GetUserId()
        };

        var deleted = await _mediator.Send(command);
        if (!deleted)
        {
            return NotFound(new { message = $"Recurring transaction with ID {id} not found." });
        }

        return NoContent();
    }

    /// <summary>
    /// Pause a recurring transaction (no reminders or auto-created transactions while paused)
    /// </summary>
    [HttpPatch("{id:int}/pause")]
    public async Task<ActionResult<RecurringTransactionDto>> PauseRecurringTransaction(int id)
    {
        return await SetActiveAsync(id, isActive: false);
    }

    /// <summary>
    /// Resume a paused recurring transaction. The next due date is fast-forwarded
    /// so occurrences missed while paused are not retroactively fired.
    /// </summary>
    [HttpPatch("{id:int}/resume")]
    public async Task<ActionResult<RecurringTransactionDto>> ResumeRecurringTransaction(int id)
    {
        return await SetActiveAsync(id, isActive: true);
    }

    private async Task<ActionResult<RecurringTransactionDto>> SetActiveAsync(int id, bool isActive)
    {
        var command = new SetRecurringTransactionActiveCommand
        {
            Id = id,
            UserId = _currentUserService.GetUserId(),
            IsActive = isActive
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
