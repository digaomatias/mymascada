using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Waitlist.Commands;
using MyMascada.Domain.Enums;
using MyMascada.WebAPI.Filters;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/admin")]
[AdminApiKey]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly IInvitationCodeRepository _invitationCodeRepository;

    public AdminController(
        IMediator mediator,
        IWaitlistRepository waitlistRepository,
        IInvitationCodeRepository invitationCodeRepository)
    {
        _mediator = mediator;
        _waitlistRepository = waitlistRepository;
        _invitationCodeRepository = invitationCodeRepository;
    }

    [HttpGet("waitlist")]
    public async Task<IActionResult> GetWaitlist([FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        WaitlistStatus? statusFilter = status.HasValue ? (WaitlistStatus)status.Value : null;
        var (items, totalCount) = await _waitlistRepository.GetPagedAsync(statusFilter, page, pageSize);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpGet("waitlist/{id:guid}")]
    public async Task<IActionResult> GetWaitlistEntry(Guid id)
    {
        var entry = await _waitlistRepository.GetByIdAsync(id);
        if (entry == null) return NotFound();
        return Ok(entry);
    }

    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations([FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        InvitationCodeStatus? statusFilter = status.HasValue ? (InvitationCodeStatus)status.Value : null;
        var (items, totalCount) = await _invitationCodeRepository.GetPagedAsync(statusFilter, page, pageSize);
        return Ok(new { items, totalCount, page, pageSize });
    }

    [HttpPost("invitations/generate")]
    public async Task<IActionResult> GenerateInvitation([FromBody] GenerateInvitationRequest request)
    {
        var command = new GenerateInvitationCommand
        {
            WaitlistEntryId = request.WaitlistEntryId,
            Email = request.Email,
            ExpiresInDays = request.ExpiresInDays ?? 7,
            MaxUses = request.MaxUses ?? 1,
            SendEmail = request.SendEmail ?? true
        };

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpDelete("invitations/{id:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid id)
    {
        var code = await _invitationCodeRepository.GetByIdAsync(id);
        if (code == null) return NotFound();

        code.Status = InvitationCodeStatus.Revoked;
        await _invitationCodeRepository.UpdateAsync(code);
        return Ok(new { message = "Code revoked" });
    }
}

public class GenerateInvitationRequest
{
    public Guid? WaitlistEntryId { get; set; }
    public string? Email { get; set; }
    public int? ExpiresInDays { get; set; }
    public int? MaxUses { get; set; }
    public bool? SendEmail { get; set; }
}
