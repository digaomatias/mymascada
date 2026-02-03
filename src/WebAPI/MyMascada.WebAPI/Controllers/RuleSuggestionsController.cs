using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Commands;
using MyMascada.Application.Features.RuleSuggestions.DTOs;
using MyMascada.Application.Features.RuleSuggestions.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RuleSuggestionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public RuleSuggestionsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get rule suggestions for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<RuleSuggestionsResponse>> GetRuleSuggestions(
        [FromQuery] bool includeProcessed = false,
        [FromQuery] int? limit = null,
        [FromQuery] double? minConfidenceThreshold = null)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var query = new GetRuleSuggestionsQuery 
            { 
                UserId = userId, 
                IncludeProcessed = includeProcessed,
                Limit = limit,
                MinConfidenceThreshold = minConfidenceThreshold
            };
            
            var response = await _mediator.Send(query);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve rule suggestions" });
        }
    }

    /// <summary>
    /// Generate new rule suggestions for the current user
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<RuleSuggestionsResponse>> GenerateRuleSuggestions(
        [FromBody] GenerateRuleSuggestionsRequest? request = null)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var command = new GenerateRuleSuggestionsCommand
            {
                UserId = userId,
                LimitSuggestions = request?.LimitSuggestions ?? 10,
                MinConfidenceThreshold = request?.MinConfidenceThreshold ?? 0.7,
                ForceRegenerate = request?.ForceRegenerate ?? false
            };

            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to generate rule suggestions" });
        }
    }

    /// <summary>
    /// Accept a rule suggestion and create a categorization rule
    /// </summary>
    [HttpPost("{suggestionId}/accept")]
    public async Task<ActionResult<int>> AcceptRuleSuggestion(
        int suggestionId,
        [FromBody] AcceptRuleSuggestionRequest? request = null)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var command = new AcceptRuleSuggestionCommand
            {
                SuggestionId = suggestionId,
                UserId = userId,
                CustomRuleName = request?.RuleName,
                CustomRuleDescription = request?.RuleDescription,
                Priority = request?.Priority
            };

            var ruleId = await _mediator.Send(command);
            return Ok(new { ruleId, message = "Rule suggestion accepted successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to accept rule suggestion" });
        }
    }

    /// <summary>
    /// Reject/dismiss a rule suggestion
    /// </summary>
    [HttpPost("{suggestionId}/reject")]
    public async Task<ActionResult> RejectRuleSuggestion(int suggestionId)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var command = new RejectRuleSuggestionCommand
            {
                SuggestionId = suggestionId,
                UserId = userId
            };

            await _mediator.Send(command);
            return Ok(new { message = "Rule suggestion rejected successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to reject rule suggestion" });
        }
    }
}