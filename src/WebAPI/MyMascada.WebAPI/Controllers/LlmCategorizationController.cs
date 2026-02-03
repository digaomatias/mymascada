using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LlmCategorizationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<LlmCategorizationController> _logger;
    private readonly ICurrentUserService _currentUserService;

    public LlmCategorizationController(IMediator mediator, ILogger<LlmCategorizationController> logger, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    [HttpPost("batch-categorize")]
    public async Task<ActionResult<BulkLlmCategorizationResult>> BatchCategorizeTransactions(
        [FromBody] BatchCategorizationRequest request)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var command = new BulkCategorizeWithLlmCommand
            {
                UserId = userId,
                TransactionIds = request.TransactionIds,
                MaxBatchSize = Math.Min(request.MaxBatchSize ?? 50, 100) // Safety limit
            };

            var result = await _mediator.Send(command);
            
            if (!result.Success)
            {
                _logger.LogWarning("Batch categorization failed for user {UserId}: {Errors}", 
                    userId, string.Join(", ", result.Errors));
                return BadRequest(new { errors = result.Errors });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during batch categorization");
            return StatusCode(500, new { error = "An error occurred while processing your request" });
        }
    }

    [HttpGet("health")]
    public async Task<ActionResult<LlmServiceHealthResponse>> GetServiceHealth()
    {
        try
        {
            var llmService = HttpContext.RequestServices.GetRequiredService<ILlmCategorizationService>();
            var isAvailable = await llmService.IsServiceAvailableAsync();
            
            return Ok(new LlmServiceHealthResponse
            {
                IsAvailable = isAvailable,
                Status = isAvailable ? "healthy" : "unavailable",
                CheckedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking LLM service health");
            return Ok(new LlmServiceHealthResponse
            {
                IsAvailable = false,
                Status = "error",
                CheckedAt = DateTime.UtcNow
            });
        }
    }
}

public class BatchCategorizationRequest
{
    public List<int> TransactionIds { get; set; } = new();
    public int? MaxBatchSize { get; set; }
}

public class LlmServiceHealthResponse
{
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}
