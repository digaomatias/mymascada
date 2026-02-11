using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AccountSharing.Commands;
using MyMascada.Application.Features.AccountSharing.DTOs;
using MyMascada.Application.Features.AccountSharing.Queries;
using MyMascada.Domain.Enums;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class AccountSharingController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUserService;

    public AccountSharingController(
        ISender mediator,
        ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Invite a user to share an account by email.
    /// </summary>
    [HttpPost("accounts/{accountId}/shares")]
    public async Task<ActionResult<CreateAccountShareResult>> CreateShare(int accountId, [FromBody] CreateAccountShareRequest request)
    {
        var command = new CreateAccountShareCommand
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = accountId,
            Email = request.Email,
            Role = request.Role
        };

        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAccountShares), new { accountId }, result);
    }

    /// <summary>
    /// List all shares for an account (owner only).
    /// </summary>
    [HttpGet("accounts/{accountId}/shares")]
    public async Task<ActionResult<List<AccountShareDto>>> GetAccountShares(int accountId)
    {
        var query = new GetAccountSharesQuery
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = accountId
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Revoke a share (owner only).
    /// </summary>
    [HttpDelete("accounts/{accountId}/shares/{shareId}")]
    public async Task<ActionResult> RevokeShare(int accountId, int shareId)
    {
        var command = new RevokeAccountShareCommand
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = accountId,
            ShareId = shareId
        };

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Update the role of an existing share (owner only).
    /// </summary>
    [HttpPatch("accounts/{accountId}/shares/{shareId}/role")]
    public async Task<ActionResult> UpdateShareRole(int accountId, int shareId, [FromBody] UpdateShareRoleRequest request)
    {
        var command = new UpdateAccountShareRoleCommand
        {
            UserId = _currentUserService.GetUserId(),
            AccountId = accountId,
            ShareId = shareId,
            NewRole = request.Role
        };

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Accept an account share invitation via token.
    /// </summary>
    [HttpPost("account-shares/accept")]
    public async Task<ActionResult<AccountShareDto>> AcceptShare([FromBody] AcceptDeclineShareRequest request)
    {
        var command = new AcceptAccountShareCommand
        {
            Token = request.Token,
            UserId = _currentUserService.GetUserId()
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Decline an account share invitation via token.
    /// </summary>
    [HttpPost("account-shares/decline")]
    public async Task<ActionResult> DeclineShare([FromBody] AcceptDeclineShareRequest request)
    {
        var command = new DeclineAccountShareCommand
        {
            Token = request.Token,
            UserId = _currentUserService.GetUserId()
        };

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Accept an account share invitation by share ID (in-app).
    /// </summary>
    [HttpPost("account-shares/{shareId:int}/accept")]
    public async Task<ActionResult<AccountShareDto>> AcceptShareById(int shareId)
    {
        var command = new AcceptAccountShareByIdCommand
        {
            ShareId = shareId,
            UserId = _currentUserService.GetUserId()
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Decline an account share invitation by share ID (in-app).
    /// </summary>
    [HttpPost("account-shares/{shareId:int}/decline")]
    public async Task<ActionResult> DeclineShareById(int shareId)
    {
        var command = new DeclineAccountShareByIdCommand
        {
            ShareId = shareId,
            UserId = _currentUserService.GetUserId()
        };

        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// List all shares received by the current user.
    /// </summary>
    [HttpGet("account-shares/received")]
    public async Task<ActionResult<List<ReceivedShareDto>>> GetReceivedShares()
    {
        var query = new GetReceivedSharesQuery
        {
            UserId = _currentUserService.GetUserId()
        };

        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
