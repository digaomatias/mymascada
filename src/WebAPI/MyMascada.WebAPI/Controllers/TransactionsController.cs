using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Enums;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public TransactionsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<TransactionListResponse>> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? accountId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] TransactionStatus? status = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isReviewed = null,
        [FromQuery] bool? isReconciled = null,
        [FromQuery] bool? isExcluded = null,
        [FromQuery] bool? needsCategorization = null,
        [FromQuery] bool? includeTransfers = null,
        [FromQuery] bool? onlyTransfers = null,
        [FromQuery] Guid? transferId = null,
        [FromQuery] string? transactionType = null,
        [FromQuery] string sortBy = "TransactionDate",
        [FromQuery] string sortDirection = "desc")
    {
        var query = new GetTransactionsQuery
        {
            UserId = _currentUserService.GetUserId(),
            Page = page,
            PageSize = Math.Min(pageSize, 100), // Limit page size
            AccountId = accountId,
            CategoryId = categoryId,
            StartDate = startDate?.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : startDate,
            EndDate = endDate?.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : endDate,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Status = status,
            SearchTerm = searchTerm,
            IsReviewed = isReviewed,
            IsReconciled = isReconciled,
            IsExcluded = isExcluded,
            NeedsCategorization = needsCategorization,
            IncludeTransfers = includeTransfers,
            OnlyTransfers = onlyTransfers,
            TransferId = transferId,
            TransactionType = transactionType,
            SortBy = sortBy,
            SortDirection = sortDirection
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionDto>> GetTransaction(int id)
    {
        var query = new GetTransactionQuery
        {
            UserId = _currentUserService.GetUserId(),
            Id = id
        };

        var result = await _mediator.Send(query);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> CreateTransaction([FromBody] CreateTransactionRequest request)
    {
        var command = new CreateTransactionCommand
        {
            UserId = _currentUserService.GetUserId(),
            Amount = request.Amount,
            TransactionDate = request.TransactionDate,
            Description = request.Description,
            UserDescription = request.UserDescription,
            Status = request.Status,
            Notes = request.Notes,
            Location = request.Location,
            Tags = request.Tags,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId
        };

        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetTransaction), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("adjustment")]
    public async Task<ActionResult<TransactionDto>> CreateAdjustmentTransaction([FromBody] CreateAdjustmentRequest request)
    {
        var command = new CreateTransactionCommand
        {
            UserId = _currentUserService.GetUserId(),
            Amount = request.Amount,
            TransactionDate = DateTime.UtcNow,
            Description = request.Description ?? "Balance adjustment",
            UserDescription = request.Description,
            Status = TransactionStatus.Cleared, // Adjustments are typically cleared immediately
            Notes = request.Notes,
            AccountId = request.AccountId,
            CategoryId = null, // Adjustments typically don't have categories
            Tags = "adjustment"
        };

        try
        {
            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetTransaction), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TransactionDto>> UpdateTransaction(int id, [FromBody] UpdateTransactionRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest("Transaction ID mismatch");
        }

        var command = new UpdateTransactionCommand
        {
            UserId = _currentUserService.GetUserId(),
            Id = request.Id,
            Amount = request.Amount,
            TransactionDate = request.TransactionDate,
            Description = request.Description,
            UserDescription = request.UserDescription,
            Status = request.Status,
            Notes = request.Notes,
            Location = request.Location,
            Tags = request.Tags,
            CategoryId = request.CategoryId
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(int id)
    {
        var command = new DeleteTransactionCommand
        {
            UserId = _currentUserService.GetUserId(),
            Id = id
        };

        var result = await _mediator.Send(command);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("recent")]
    public async Task<ActionResult<List<TransactionDto>>> GetRecentTransactions([FromQuery] int count = 10)
    {
        var query = new GetTransactionsQuery
        {
            UserId = _currentUserService.GetUserId(),
            Page = 1,
            PageSize = Math.Min(count, 50),
            SortBy = "TransactionDate",
            SortDirection = "desc"
        };

        var result = await _mediator.Send(query);
        return Ok(result.Transactions);
    }

    [HttpPatch("{id}/review")]
    public async Task<IActionResult> ReviewTransaction(int id)
    {
        var command = new ReviewTransactionCommand
        {
            UserId = _currentUserService.GetUserId(),
            TransactionId = id
        };

        try
        {
            var result = await _mediator.Send(command);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("review-all")]
    public async Task<ActionResult<ReviewAllTransactionsResponse>> ReviewAllTransactions()
    {
        var command = new ReviewAllTransactionsCommand
        {
            UserId = _currentUserService.GetUserId()
        };

        var result = await _mediator.Send(command);
        
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    [HttpPost("bulk-review-categorized")]
    public async Task<ActionResult<BulkReviewCategorizedTransactionsResult>> BulkReviewCategorized(
        [FromQuery] int? accountId = null,
        [FromQuery] string? searchText = null)
    {
        var command = new BulkReviewCategorizedTransactionsCommand
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = accountId,
            SearchText = searchText
        };

        var result = await _mediator.Send(command);
        
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    [HttpGet("description-suggestions")]
    public async Task<ActionResult<IEnumerable<string>>> GetDescriptionSuggestions([FromQuery] string? q = null, [FromQuery] int limit = 10)
    {
        var query = new GetDescriptionSuggestionsQuery
        {
            UserId = _currentUserService.GetUserId(),
            SearchTerm = q,
            Limit = limit
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<object>>> GetCategoriesInTransactions(
        [FromQuery] int? accountId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] TransactionStatus? status = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isReviewed = null,
        [FromQuery] bool? isExcluded = null,
        [FromQuery] bool? includeTransfers = null,
        [FromQuery] bool? onlyTransfers = null,
        [FromQuery] Guid? transferId = null)
    {
        var query = new GetCategoriesInTransactionsQuery
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = accountId,
            StartDate = startDate,
            EndDate = endDate,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            Status = status,
            SearchTerm = searchTerm,
            IsReviewed = isReviewed,
            IsExcluded = isExcluded,
            IncludeTransfers = includeTransfers,
            OnlyTransfers = onlyTransfers,
            TransferId = transferId
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("duplicates")]
    public async Task<ActionResult<DuplicateTransactionsResponse>> GetDuplicateTransactions(
        [FromQuery] decimal amountTolerance = 0.01m,
        [FromQuery] int dateToleranceDays = 1,
        [FromQuery] bool includeReviewed = false,
        [FromQuery] bool sameAccountOnly = false,
        [FromQuery] decimal minConfidence = 0.5m)
    {
        var query = new GetDuplicateTransactionsQuery
        {
            UserId = _currentUserService.GetUserId(),
            AmountTolerance = amountTolerance,
            DateToleranceDays = dateToleranceDays,
            IncludeReviewed = includeReviewed,
            SameAccountOnly = sameAccountOnly,
            MinConfidence = minConfidence
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("duplicates/resolve")]
    public async Task<ActionResult<ResolveDuplicatesResponse>> ResolveDuplicates([FromBody] BulkResolveDuplicatesRequest request)
    {
        var command = new ResolveDuplicatesCommand
        {
            UserId = _currentUserService.GetUserId(),
            Resolutions = request.Resolutions.Select(r => new DuplicateResolutionItem
            {
                GroupId = r.GroupId.ToString(),
                TransactionIdsToKeep = r.TransactionIdsToKeep,
                TransactionIdsToDelete = r.TransactionIdsToDelete,
                MarkAsNotDuplicate = r.MarkAsNotDuplicate,
                Notes = r.Notes
            }).ToList()
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("potential-transfers")]
    public async Task<ActionResult<PotentialTransfersResponse>> GetPotentialTransfers(
        [FromQuery] decimal amountTolerance = 0.01m,
        [FromQuery] int dateToleranceDays = 3,
        [FromQuery] bool includeReviewed = false,
        [FromQuery] decimal minConfidence = 0.5m,
        [FromQuery] bool includeExistingTransfers = false)
    {
        var query = new GetPotentialTransfersQuery
        {
            UserId = _currentUserService.GetUserId(),
            AmountTolerance = amountTolerance,
            DateToleranceDays = dateToleranceDays,
            IncludeReviewed = includeReviewed,
            MinConfidence = minConfidence,
            IncludeExistingTransfers = includeExistingTransfers
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("transfers/create-missing")]
    public async Task<ActionResult<ConfirmTransfersResponse>> CreateMissingTransfer([FromBody] CreateMissingTransferRequest request)
    {
        var command = new CreateMissingTransferCommand
        {
            UserId = _currentUserService.GetUserId(),
            ExistingTransactionId = request.ExistingTransactionId,
            MissingAccountId = request.MissingAccountId,
            Description = request.Description,
            Notes = request.Notes,
            TransactionDate = request.TransactionDate?.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(request.TransactionDate.Value, DateTimeKind.Utc) 
                : request.TransactionDate
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("transfers/link")]
    public async Task<ActionResult<ConfirmTransfersResponse>> LinkTransactionsAsTransfer([FromBody] LinkTransactionsAsTransferRequest request)
    {
        var command = new LinkTransactionsAsTransferCommand
        {
            UserId = _currentUserService.GetUserId(),
            SourceTransactionId = request.SourceTransactionId,
            DestinationTransactionId = request.DestinationTransactionId,
            Description = request.Description,
            Notes = request.Notes
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("bulk-delete")]
    public async Task<ActionResult<BulkDeleteTransactionsResponse>> BulkDeleteTransactions([FromBody] BulkDeleteTransactionsRequest request)
    {
        var command = new BulkDeleteTransactionsCommand
        {
            UserId = _currentUserService.GetUserId(),
            TransactionIds = request.TransactionIds,
            Reason = request.Reason
        };

        try
        {
            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("normalize-amounts")]
    public async Task<ActionResult<BulkNormalizeTransactionAmountsResult>> NormalizeTransactionAmounts()
    {
        var command = new BulkNormalizeTransactionAmountsCommand { UserId = _currentUserService.GetUserId() };

        try
        {
            var result = await _mediator.Send(command);
            
            if (result.IsSuccess)
            {
                return Ok(result);
            }
            else
            {
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new {
                message = "An error occurred during transaction amount normalization"
            });
        }
    }
}