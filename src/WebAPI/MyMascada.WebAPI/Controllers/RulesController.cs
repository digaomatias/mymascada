using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Rules.Commands;
using MyMascada.Application.Features.Rules.DTOs;
using MyMascada.Application.Features.Rules.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RulesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public RulesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all categorization rules for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategorizationRuleDto>>> GetRules(
        [FromQuery] bool includeInactive = false)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var query = new GetRulesQuery { UserId = userId, IncludeInactive = includeInactive };
            var rules = await _mediator.Send(query);
            return Ok(rules);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve rules" });
        }
    }

    /// <summary>
    /// Get a specific rule by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CategorizationRuleDto>> GetRule(int id)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var query = new GetRuleByIdQuery { RuleId = id, UserId = userId };
            var rule = await _mediator.Send(query);
            
            if (rule == null)
                return NotFound(new { error = "Rule not found" });
                
            return Ok(rule);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve rule" });
        }
    }

    /// <summary>
    /// Create a new categorization rule
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CategorizationRuleDto>> CreateRule([FromBody] CreateRuleCommand command)
    {
        try
        {
            command.UserId = _currentUserService.GetUserId();
            var rule = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to create rule" });
        }
    }

    /// <summary>
    /// Update an existing categorization rule
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<CategorizationRuleDto>> UpdateRule(int id, [FromBody] UpdateRuleCommand command)
    {
        try
        {
            command.RuleId = id;
            command.UserId = _currentUserService.GetUserId();
            var rule = await _mediator.Send(command);
            return Ok(rule);
        }
        catch (ArgumentException)
        {
            return NotFound(new { error = "Rule not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to update rule" });
        }
    }

    /// <summary>
    /// Delete a categorization rule
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteRule(int id)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var command = new DeleteRuleCommand { RuleId = id, UserId = userId };
            await _mediator.Send(command);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to delete rule" });
        }
    }

    /// <summary>
    /// Test a rule against existing transactions
    /// </summary>
    [HttpPost("{id}/test")]
    public async Task<ActionResult<RuleTestResultDto>> TestRule(int id, [FromQuery] int maxResults = 50)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var command = new TestRuleCommand { RuleId = id, UserId = userId, MaxResults = maxResults };
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to test rule" });
        }
    }

    /// <summary>
    /// Get rule statistics for the current user
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<RuleStatisticsDto>> GetRuleStatistics()
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var query = new GetRuleStatisticsQuery { UserId = userId };
            var statistics = await _mediator.Send(query);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get rule statistics" });
        }
    }

    /// <summary>
    /// Update rule priorities
    /// </summary>
    [HttpPut("priorities")]
    public async Task<ActionResult> UpdateRulePriorities([FromBody] UpdateRulePrioritiesCommand command)
    {
        try
        {
            command.UserId = _currentUserService.GetUserId();
            await _mediator.Send(command);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to update rule priorities" });
        }
    }

    /// <summary>
    /// Get rule suggestions based on transaction patterns
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<RuleSuggestionsResponse>> GetRuleSuggestions()
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            var query = new GetRuleSuggestionsQuery { UserId = userId };
            var suggestions = await _mediator.Send(query);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to get rule suggestions" });
        }
    }

    /// <summary>
    /// Analyze specific transactions for rule suggestions
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<List<RuleSuggestionDto>>> AnalyzeTransactions([FromBody] AnalyzeTransactionsForRulesQuery query)
    {
        try
        {
            query.UserId = _currentUserService.GetUserId();
            var suggestions = await _mediator.Send(query);
            return Ok(suggestions);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to analyze transactions" });
        }
    }

    /// <summary>
    /// Create a rule from a suggestion
    /// </summary>
    [HttpPost("from-suggestion")]
    public async Task<ActionResult<CategorizationRuleDto>> CreateRuleFromSuggestion([FromBody] CreateRuleFromSuggestionCommand command)
    {
        try
        {
            command.UserId = _currentUserService.GetUserId();
            var rule = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to create rule from suggestion" });
        }
    }

}