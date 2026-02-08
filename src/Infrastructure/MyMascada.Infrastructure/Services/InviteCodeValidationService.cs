using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services;

public class InviteCodeValidationService : IInviteCodeValidationService
{
    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly BetaAccessOptions _betaAccessOptions;
    private readonly ILogger<InviteCodeValidationService> _logger;

    public InviteCodeValidationService(
        IInvitationCodeRepository invitationCodeRepository,
        IWaitlistRepository waitlistRepository,
        IOptions<BetaAccessOptions> betaAccessOptions,
        ILogger<InviteCodeValidationService> logger)
    {
        _invitationCodeRepository = invitationCodeRepository;
        _waitlistRepository = waitlistRepository;
        _betaAccessOptions = betaAccessOptions.Value;
        _logger = logger;
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateAsync(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, "A valid invite code is required to register during the beta period.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();

        // Check database first
        var invitationCode = await _invitationCodeRepository.GetByNormalizedCodeAsync(normalizedCode);
        if (invitationCode != null)
        {
            if (invitationCode.Status != InvitationCodeStatus.Active)
            {
                _logger.LogWarning("Invite code {Code} has status {Status}", normalizedCode, invitationCode.Status);
                return (false, "This invite code is no longer valid.");
            }

            if (invitationCode.ExpiresAt <= DateTime.UtcNow)
            {
                _logger.LogWarning("Invite code {Code} has expired", normalizedCode);
                return (false, "This invite code has expired.");
            }

            if (invitationCode.UseCount >= invitationCode.MaxUses)
            {
                _logger.LogWarning("Invite code {Code} has reached max uses ({MaxUses})", normalizedCode, invitationCode.MaxUses);
                return (false, "This invite code has already been used.");
            }

            return (true, null);
        }

        // Fall back to env-var codes for backward compatibility
        var validCodes = _betaAccessOptions.GetValidCodes();
        if (validCodes.Any(c => string.Equals(c, code.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return (true, null);
        }

        return (false, "A valid invite code is required to register during the beta period.");
    }

    public async Task<bool> ClaimAsync(string code, Guid userId)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var invitationCode = await _invitationCodeRepository.GetByNormalizedCodeAsync(normalizedCode);

        if (invitationCode == null)
        {
            // Might be an env-var code, which doesn't need claiming
            return false;
        }

        invitationCode.UseCount++;
        invitationCode.ClaimedByUserId = userId;
        invitationCode.ClaimedAt = DateTime.UtcNow;

        if (invitationCode.UseCount >= invitationCode.MaxUses)
        {
            invitationCode.Status = InvitationCodeStatus.Claimed;
        }

        // Update linked waitlist entry if present
        if (invitationCode.WaitlistEntryId.HasValue)
        {
            var waitlistEntry = await _waitlistRepository.GetByIdAsync(invitationCode.WaitlistEntryId.Value);
            if (waitlistEntry != null)
            {
                waitlistEntry.Status = WaitlistStatus.Registered;
                waitlistEntry.RegisteredAt = DateTime.UtcNow;
                await _waitlistRepository.UpdateAsync(waitlistEntry);
            }
        }

        await _invitationCodeRepository.UpdateAsync(invitationCode);
        _logger.LogInformation("Invite code {Code} claimed by user {UserId}", normalizedCode, userId);

        return true;
    }
}
