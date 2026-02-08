using System.Security.Cryptography;
using System.Text;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.Commands;

/// <summary>
/// Command to decline a pending account share invitation via token.
/// </summary>
public class DeclineAccountShareCommand : IRequest<Unit>
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}

public class DeclineAccountShareCommandHandler : IRequestHandler<DeclineAccountShareCommand, Unit>
{
    private readonly IAccountShareRepository _accountShareRepository;

    public DeclineAccountShareCommandHandler(IAccountShareRepository accountShareRepository)
    {
        _accountShareRepository = accountShareRepository;
    }

    public async Task<Unit> Handle(DeclineAccountShareCommand request, CancellationToken cancellationToken)
    {
        // Hash the raw token to find the matching share
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token))).ToLower();

        var share = await _accountShareRepository.GetByInvitationTokenAsync(tokenHash);
        if (share == null)
            throw new ArgumentException("Invalid or expired invitation token.");

        // Validate the share belongs to this user
        if (share.SharedWithUserId != request.UserId)
            throw new UnauthorizedAccessException("This invitation was not sent to you.");

        // Validate not expired
        if (share.InvitationExpiresAt.HasValue && share.InvitationExpiresAt.Value < DateTimeProvider.UtcNow)
            throw new InvalidOperationException("This invitation has expired.");

        // Validate status is Pending
        if (share.Status != AccountShareStatus.Pending)
            throw new InvalidOperationException($"This invitation cannot be declined because its status is '{share.Status}'.");

        // Decline the share
        share.Status = AccountShareStatus.Declined;
        share.InvitationToken = null;
        share.UpdatedAt = DateTimeProvider.UtcNow;

        await _accountShareRepository.UpdateAsync(share);

        return Unit.Value;
    }
}
