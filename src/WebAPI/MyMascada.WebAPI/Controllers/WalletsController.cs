using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.Commands;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Application.Features.Wallets.Queries;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/latest/[controller]")]
[Authorize]
public class WalletsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public WalletsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get the wallet dashboard summary
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<WalletDashboardSummaryDto>> GetDashboard()
    {
        try
        {
            var query = new GetWalletDashboardQuery
            {
                UserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the wallet dashboard." });
        }
    }

    /// <summary>
    /// Get all wallets for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WalletSummaryDto>>> GetWallets(
        [FromQuery] bool includeArchived = false)
    {
        try
        {
            var query = new GetWalletsQuery
            {
                UserId = _currentUserService.GetUserId(),
                IncludeArchived = includeArchived
            };

            var wallets = await _mediator.Send(query);
            return Ok(wallets);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving wallets." });
        }
    }

    /// <summary>
    /// Get a specific wallet by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<WalletDetailDto>> GetWallet(int id)
    {
        try
        {
            var query = new GetWalletQuery
            {
                WalletId = id,
                UserId = _currentUserService.GetUserId()
            };

            var wallet = await _mediator.Send(query);

            if (wallet == null)
            {
                return NotFound(new { message = "Wallet not found or you don't have permission to access it." });
            }

            return Ok(wallet);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving the wallet." });
        }
    }

    /// <summary>
    /// Create a new wallet
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WalletDetailDto>> CreateWallet([FromBody] CreateWalletRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new CreateWalletCommand
            {
                Name = request.Name,
                Icon = request.Icon,
                Color = request.Color,
                Currency = request.Currency,
                TargetAmount = request.TargetAmount,
                UserId = _currentUserService.GetUserId()
            };

            var wallet = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetWallet), new { id = wallet.Id }, wallet);
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
            return StatusCode(500, new { message = "An error occurred while creating the wallet." });
        }
    }

    /// <summary>
    /// Update an existing wallet
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<WalletDetailDto>> UpdateWallet(int id, [FromBody] UpdateWalletRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new UpdateWalletCommand
            {
                WalletId = id,
                Name = request.Name,
                Icon = request.Icon,
                Color = request.Color,
                Currency = request.Currency,
                IsArchived = request.IsArchived,
                TargetAmount = request.TargetAmount,
                ClearTargetAmount = request.ClearTargetAmount,
                UserId = _currentUserService.GetUserId()
            };

            var wallet = await _mediator.Send(command);
            return Ok(wallet);
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
            return StatusCode(500, new { message = "An error occurred while updating the wallet." });
        }
    }

    /// <summary>
    /// Delete a wallet (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteWallet(int id)
    {
        try
        {
            var command = new DeleteWalletCommand
            {
                WalletId = id,
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
            return StatusCode(500, new { message = "An error occurred while deleting the wallet." });
        }
    }

    /// <summary>
    /// Create a new allocation for a wallet
    /// </summary>
    [HttpPost("{id}/allocations")]
    public async Task<ActionResult<WalletAllocationDto>> CreateAllocation(int id, [FromBody] CreateAllocationRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var command = new CreateWalletAllocationCommand
            {
                WalletId = id,
                TransactionId = request.TransactionId,
                Amount = request.Amount,
                Note = request.Note,
                UserId = _currentUserService.GetUserId()
            };

            var allocation = await _mediator.Send(command);
            return StatusCode(201, allocation);
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
            return StatusCode(500, new { message = "An error occurred while creating the allocation." });
        }
    }

    /// <summary>
    /// Delete an allocation from a wallet
    /// </summary>
    [HttpDelete("{walletId}/allocations/{allocationId}")]
    public async Task<ActionResult> DeleteAllocation(int walletId, int allocationId)
    {
        try
        {
            var command = new DeleteWalletAllocationCommand
            {
                WalletId = walletId,
                AllocationId = allocationId,
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
            return StatusCode(500, new { message = "An error occurred while deleting the allocation." });
        }
    }
}
