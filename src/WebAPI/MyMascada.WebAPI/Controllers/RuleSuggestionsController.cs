using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Commands;
using MyMascada.Application.Features.RuleSuggestions.DTOs;
using MyMascada.Application.Features.RuleSuggestions.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/latest/[controller]")]
[Authorize]
public class RuleSuggestionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IRuleSuggestionRepository _ruleSuggestionRepository;

    public RuleSuggestionsController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        IRuleSuggestionRepository ruleSuggestionRepository)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _ruleSuggestionRepository = ruleSuggestionRepository;
    }

    /// <summary>
    /// Returns a lightweight count of pending rule suggestions for the
    /// current user. Used by the sidebar "Rules" link to drive the badge
    /// without materializing the full suggestion graph (SampleTransactions,
    /// SuggestedCategory, etc.) that the main listing endpoint loads.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<RuleSuggestionsCountSummary>> GetRuleSuggestionsSummary(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.GetUserId();
        var totalSuggestions = await _ruleSuggestionRepository
            .CountPendingSuggestionsAsync(userId, cancellationToken);

        return Ok(new RuleSuggestionsCountSummary
        {
            TotalSuggestions = totalSuggestions
        });
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
    /// Generate new rule suggestions for the current user.
    /// AI-enhanced generation requires a Pro subscription or self-hosted deployment.
    /// Free users still get basic (deterministic) suggestions.
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<RuleSuggestionsResponse>> GenerateRuleSuggestions(
        [FromBody] GenerateRuleSuggestionsRequest? request = null)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            // No 403 gate here — RuleSuggestionService checks the tier internally
            // and falls back to deterministic (basic) suggestions when AI is unavailable.
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

/// <summary>
/// Lightweight response for the sidebar badge — only the count, no
/// suggestion graph. Named distinctly from `RuleSuggestionsSummaryDto` so
/// the full-summary shape used by the main listing endpoint stays intact.
/// </summary>
public class RuleSuggestionsCountSummary
{
    public int TotalSuggestions { get; set; }
}