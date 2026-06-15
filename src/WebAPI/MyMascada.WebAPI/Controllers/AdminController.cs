using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Waitlist.Commands;
using MyMascada.Domain.Enums;
using MyMascada.WebAPI.Filters;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Route("api/latest/admin")]
[AdminApiKey]
public class AdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly IAiTokenTracker _aiTokenTracker;
    private readonly IUserRepository _userRepository;
    private readonly IUserFeatureService _userFeatures;

    public AdminController(
        IMediator mediator,
        IWaitlistRepository waitlistRepository,
        IInvitationCodeRepository invitationCodeRepository,
        IAiTokenTracker aiTokenTracker,
        IUserRepository userRepository,
        IUserFeatureService userFeatures)
    {
        _mediator = mediator;
        _waitlistRepository = waitlistRepository;
        _invitationCodeRepository = invitationCodeRepository;
        _aiTokenTracker = aiTokenTracker;
        _userRepository = userRepository;
        _userFeatures = userFeatures;
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

    [HttpGet("ai-usage")]
    public async Task<IActionResult> GetAiUsage([FromQuery] int days = 30)
    {
        var from = DateTime.UtcNow.Date.AddDays(-days);
        var to = DateTime.UtcNow;

        var summary = await _aiTokenTracker.GetAdminSummaryAsync(from, to);
        return Ok(summary);
    }

    /// <summary>
    /// Look up a user's per-feature state (launch default, override, effective) by
    /// email. Used by the admin module to enable/disable gated features per user
    /// (e.g. turn on bank connections for a single account).
    /// </summary>
    [HttpGet("users/features")]
    public async Task<IActionResult> GetUserFeatures([FromQuery] string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "email is required" });

        var user = await _userRepository.GetByEmailAsync(email.Trim());
        if (user == null) return NotFound(new { message = "User not found" });

        var features = await _userFeatures.GetAdminViewAsync(user.Id, cancellationToken);
        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            features
        });
    }

    /// <summary>
    /// Set or clear a per-user feature override. <c>enabled = null</c> clears the
    /// override so the feature falls back to its launch default.
    /// </summary>
    [HttpPut("users/features")]
    public async Task<IActionResult> SetUserFeature([FromBody] SetUserFeatureRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest(new { message = "email is required" });
        if (string.IsNullOrWhiteSpace(request.FeatureKey)) return BadRequest(new { message = "featureKey is required" });

        var user = await _userRepository.GetByEmailAsync(request.Email.Trim());
        if (user == null) return NotFound(new { message = "User not found" });

        try
        {
            await _userFeatures.SetOverrideAsync(user.Id, request.FeatureKey, request.Enabled, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var features = await _userFeatures.GetAdminViewAsync(user.Id, cancellationToken);
        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            features
        });
    }
}

public class SetUserFeatureRequest
{
    public string Email { get; set; } = string.Empty;
    public string FeatureKey { get; set; } = string.Empty;
    /// <summary>true = force on, false = force off, null = clear override.</summary>
    public bool? Enabled { get; set; }
}

public class GenerateInvitationRequest
{
    public Guid? WaitlistEntryId { get; set; }
    public string? Email { get; set; }
    public int? ExpiresInDays { get; set; }
    public int? MaxUses { get; set; }
    public bool? SendEmail { get; set; }
}
