using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Reconciliation.Commands;
using MyMascada.Application.Features.Reconciliation.DTOs;
using MyMascada.Application.Features.Reconciliation.Queries;
using AkahuReconciliationRequest = MyMascada.Application.Features.Reconciliation.DTOs.CreateAkahuReconciliationRequest;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReconciliationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public ReconciliationController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all reconciliations for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReconciliationDto>>> GetReconciliations(
        [FromQuery] int? accountId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var query = new GetReconciliationsQuery
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = accountId,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific reconciliation by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ReconciliationDto>> GetReconciliation(int id)
    {
        var query = new GetReconciliationQuery
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id
        };

        var result = await _mediator.Send(query);
        if (result == null)
        {
            return NotFound($"Reconciliation with ID {id} not found");
        }

        return Ok(result);
    }

    /// <summary>
    /// Get reconciliation items for a specific reconciliation
    /// </summary>
    [HttpGet("{id}/items")]
    public async Task<ActionResult<IEnumerable<ReconciliationItemDto>>> GetReconciliationItems(
        int id,
        [FromQuery] MyMascada.Domain.Enums.ReconciliationItemType? itemType = null,
        [FromQuery] decimal? minConfidence = null,
        [FromQuery] MyMascada.Domain.Enums.MatchMethod? matchMethod = null)
    {
        var query = new GetReconciliationItemsQuery
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id,
            ItemType = itemType,
            MinConfidence = minConfidence,
            MatchMethod = matchMethod
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get detailed reconciliation data with categorized items for review
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<ReconciliationDetailsDto>> GetReconciliationDetails(
        int id,
        [FromQuery] string? searchTerm = null,
        [FromQuery] MyMascada.Domain.Enums.ReconciliationItemType? filterByType = null,
        [FromQuery] MyMascada.Domain.Enums.MatchMethod? filterByMatchMethod = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var query = new GetReconciliationDetailsQuery
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id,
            SearchTerm = searchTerm,
            FilterByType = filterByType,
            FilterByMatchMethod = filterByMatchMethod,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Create a new reconciliation
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ReconciliationDto>> CreateReconciliation(
        [FromBody] CreateReconciliationRequest request)
    {
        var command = new CreateReconciliationCommand
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = request.AccountId,
            StatementEndDate = request.StatementEndDate,
            StatementEndBalance = request.StatementEndBalance,
            Notes = request.Notes
        };

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetReconciliation), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing reconciliation
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ReconciliationDto>> UpdateReconciliation(
        int id,
        [FromBody] UpdateReconciliationRequest request)
    {
        var command = new UpdateReconciliationCommand
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id,
            StatementEndDate = request.StatementEndDate,
            StatementEndBalance = request.StatementEndBalance,
            Status = request.Status,
            Notes = request.Notes
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Match transactions for a reconciliation
    /// </summary>
    [HttpPost("{id}/match-transactions")]
    public async Task<ActionResult<MatchingResultDto>> MatchTransactions(
        int id,
        [FromBody] MatchTransactionsRequest request)
    {
        var command = new MatchTransactionsCommand
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id,
            BankTransactions = request.BankTransactions,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ToleranceAmount = request.ToleranceAmount,
            UseDescriptionMatching = request.UseDescriptionMatching,
            UseDateRangeMatching = request.UseDateRangeMatching,
            DateRangeToleranceDays = request.DateRangeToleranceDays
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Manually match a system transaction with a bank transaction
    /// </summary>
    [HttpPost("{id}/manual-match")]
    public async Task<ActionResult<ReconciliationItemDetailDto>> ManualMatchTransaction(
        int id,
        [FromBody] ManualMatchRequest request)
    {
        var command = new ManualMatchTransactionCommand
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id,
            SystemTransactionId = request.SystemTransactionId,
            BankTransaction = request.BankTransaction,
            Notes = request.Notes
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Unlink a matched transaction back into separate unmatched items
    /// </summary>
    [HttpDelete("items/{itemId}/unlink")]
    public async Task<ActionResult> UnlinkTransaction(int itemId)
    {
        var command = new UnlinkTransactionCommand
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationItemId = itemId
        };

        var result = await _mediator.Send(command);
        if (result)
        {
            return NoContent();
        }

        return BadRequest("Failed to unlink transaction");
    }

    /// <summary>
    /// Bulk approve matches based on confidence threshold or specific items.
    /// Approved matches will have their transactions enriched with bank data and
    /// uncategorized transactions will have bank category mappings applied.
    /// </summary>
    [HttpPost("{id}/bulk-approve")]
    public async Task<ActionResult<BulkApproveResult>> BulkApproveMatches(
        int id,
        [FromBody] BulkApproveRequest request)
    {
        var command = new BulkApproveMatchesCommand
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id,
            MinConfidenceThreshold = request.MinConfidenceThreshold,
            SpecificItemIds = request.SpecificItemIds
        };

        var result = await _mediator.Send(command);
        return Ok(new BulkApproveResult
        {
            ApprovedCount = result.ApprovedCount,
            EnrichedCount = result.EnrichedCount,
            CategorizedCount = result.CategorizedCount,
            SkippedCount = result.SkippedCount,
            Errors = result.Errors
        });
    }

    /// <summary>
    /// Finalize the reconciliation process
    /// </summary>
    [HttpPost("{id}/finalize")]
    public async Task<ActionResult<ReconciliationDto>> FinalizeReconciliation(
        int id,
        [FromBody] FinalizeReconciliationRequest request)
    {
        var command = new FinalizeReconciliationCommand
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id,
            Notes = request.Notes,
            ForceFinalize = request.ForceFinalize
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Check if Akahu reconciliation is available for an account
    /// </summary>
    [HttpGet("{accountId}/akahu-availability")]
    public async Task<ActionResult<AkahuAvailabilityResponse>> GetAkahuAvailability(int accountId)
    {
        var query = new GetAkahuReconciliationAvailabilityQuery
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = accountId
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Create a reconciliation from Akahu bank data
    /// </summary>
    [HttpPost("akahu")]
    public async Task<ActionResult<AkahuReconciliationResponse>> CreateAkahuReconciliation(
        [FromBody] AkahuReconciliationRequest request)
    {
        var command = new CreateAkahuReconciliationCommand
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = request.AccountId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            StatementEndBalance = request.StatementEndBalance,
            Notes = request.Notes
        };

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetReconciliation), new { id = result.ReconciliationId }, result);
    }

    /// <summary>
    /// Import unmatched bank transactions as new MyMascada transactions
    /// </summary>
    [HttpPost("{id}/import-unmatched")]
    public async Task<ActionResult<ImportUnmatchedResult>> ImportUnmatchedTransactions(
        int id,
        [FromBody] ImportUnmatchedApiRequest request)
    {
        var command = new ImportUnmatchedTransactionsCommand
        {
            UserId = _currentUserService.GetUserId(),
            ReconciliationId = id,
            ItemIds = request.ItemIds,
            ImportAll = request.ImportAll
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}

/// <summary>
/// Request model for importing unmatched transactions
/// </summary>
public class ImportUnmatchedApiRequest
{
    public IEnumerable<int>? ItemIds { get; set; }
    public bool ImportAll { get; set; }
}

/// <summary>
/// Request model for creating a new reconciliation
/// </summary>
public class CreateReconciliationRequest
{
    public int AccountId { get; set; }
    public DateTime StatementEndDate { get; set; }
    public decimal StatementEndBalance { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for updating a reconciliation
/// </summary>
public class UpdateReconciliationRequest
{
    public DateTime? StatementEndDate { get; set; }
    public decimal? StatementEndBalance { get; set; }
    public MyMascada.Domain.Enums.ReconciliationStatus? Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for matching transactions
/// </summary>
public class MatchTransactionsRequest
{
    public IEnumerable<BankTransactionDto> BankTransactions { get; set; } = new List<BankTransactionDto>();
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal ToleranceAmount { get; set; } = 0.01m;
    public bool UseDescriptionMatching { get; set; } = true;
    public bool UseDateRangeMatching { get; set; } = true;
    public int DateRangeToleranceDays { get; set; } = 2;
}

/// <summary>
/// Request model for manually matching transactions
/// </summary>
public class ManualMatchRequest
{
    public int? SystemTransactionId { get; set; }
    public BankTransactionDto? BankTransaction { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for bulk approve operations
/// </summary>
public class BulkApproveRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("minConfidenceThreshold")]
    public decimal MinConfidenceThreshold { get; set; } = 0.95m;

    [System.Text.Json.Serialization.JsonPropertyName("specificItemIds")]
    public IEnumerable<int>? SpecificItemIds { get; set; }
}

/// <summary>
/// Response model for bulk approve operations
/// </summary>
public class BulkApproveResult
{
    public int ApprovedCount { get; set; }
    public int EnrichedCount { get; set; }
    public int CategorizedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Request model for finalizing reconciliation
/// </summary>
public class FinalizeReconciliationRequest
{
    public string? Notes { get; set; }
    public bool ForceFinalize { get; set; } = false;
}