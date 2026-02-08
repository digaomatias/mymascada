using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transfers.Commands;
using MyMascada.Application.Features.Transfers.DTOs;
using MyMascada.Domain.Common;

namespace MyMascada.WebAPI.Controllers;

/// <summary>
/// API controller for managing transfers between accounts
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransferController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITransferRedactionService _redactionService;

    public TransferController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        ITransferRedactionService redactionService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _redactionService = redactionService;
    }

    /// <summary>
    /// Create a new transfer between accounts
    /// </summary>
    /// <param name="request">Transfer creation request</param>
    /// <returns>Created transfer information</returns>
    [HttpPost]
    public async Task<ActionResult<TransferDto>> CreateTransfer([FromBody] CreateTransferRequest request)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var command = new CreateTransferCommand
            {
                SourceAccountId = request.SourceAccountId,
                DestinationAccountId = request.DestinationAccountId,
                Amount = request.Amount,
                Currency = request.Currency,
                ExchangeRate = request.ExchangeRate,
                FeeAmount = request.FeeAmount,
                Description = request.Description,
                Notes = request.Notes,
                TransferDate = request.TransferDate,
                UserId = userId
            };

            var result = await _mediator.Send(command);
            var redacted = await _redactionService.RedactForViewerAsync(result, userId);
            return CreatedAtAction(nameof(GetTransfer), new { id = redacted.Id }, redacted);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the transfer" });
        }
    }

    /// <summary>
    /// Get a specific transfer by ID
    /// </summary>
    /// <param name="id">Transfer ID</param>
    /// <returns>Transfer information</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<TransferDto>> GetTransfer(int id)
    {
        // TODO: Implement GetTransferQuery when needed
        return NotFound(new { message = "Transfer retrieval not yet implemented" });
    }

    /// <summary>
    /// Get transfers for the current user
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50)</param>
    /// <param name="sourceAccountId">Filter by source account</param>
    /// <param name="destinationAccountId">Filter by destination account</param>
    /// <param name="startDate">Filter by start date</param>
    /// <param name="endDate">Filter by end date</param>
    /// <param name="status">Filter by transfer status</param>
    /// <returns>Paginated list of transfers</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<TransferDto>>> GetTransfers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? sourceAccountId = null,
        [FromQuery] int? destinationAccountId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? status = null)
    {
        // TODO: Implement GetTransfersQuery when needed
        return Ok(new PagedResult<TransferDto>
        {
            Items = new List<TransferDto>(),
            TotalCount = 0,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Reverse the direction of a transfer (swap source and destination)
    /// </summary>
    /// <param name="transferId">The ID of the transfer to reverse</param>
    /// <returns>Result of the reversal operation</returns>
    [HttpPost("{transferId}/reverse")]
    public async Task<ActionResult<ReverseTransferResponse>> ReverseTransfer(Guid transferId)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var command = new ReverseTransferCommand
            {
                TransferId = transferId,
                UserId = userId
            };

            var result = await _mediator.Send(command);
            
            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while reversing the transfer" });
        }
    }
}

/// <summary>
/// Request model for creating transfers
/// </summary>
public class CreateTransferRequest
{
    /// <summary>
    /// Source account ID (money leaving)
    /// </summary>
    public int SourceAccountId { get; set; }

    /// <summary>
    /// Destination account ID (money arriving)
    /// </summary>
    public int DestinationAccountId { get; set; }

    /// <summary>
    /// Transfer amount (always positive)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code for the transfer
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Exchange rate if transferring between different currencies
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Fee amount for the transfer
    /// </summary>
    public decimal? FeeAmount { get; set; }

    /// <summary>
    /// Description or reason for the transfer
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Additional notes about the transfer
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Date when the transfer occurred
    /// </summary>
    public DateTime TransferDate { get; set; } = DateTimeProvider.UtcNow;
}

/// <summary>
/// Generic paginated result wrapper
/// </summary>
public class PagedResult<T>
{
    /// <summary>
    /// List of items for current page
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Current page number
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>
    /// Whether there are more pages after this one
    /// </summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Whether there are pages before this one
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}