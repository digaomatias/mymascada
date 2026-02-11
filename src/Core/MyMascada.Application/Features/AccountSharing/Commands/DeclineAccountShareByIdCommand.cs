using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.Commands;

/// <summary>
/// Command to decline a pending account share invitation by share ID (for in-app use).
/// </summary>
public class DeclineAccountShareByIdCommand : IRequest<Unit>
{
    public int ShareId { get; set; }
    public Guid UserId { get; set; }
}

public class DeclineAccountShareByIdCommandHandler : IRequestHandler<DeclineAccountShareByIdCommand, Unit>
{
    private readonly IAccountShareRepository _accountShareRepository;

    public DeclineAccountShareByIdCommandHandler(IAccountShareRepository accountShareRepository)
    {
        _accountShareRepository = accountShareRepository;
    }

    public async Task<Unit> Handle(DeclineAccountShareByIdCommand request, CancellationToken cancellationToken)
    {
        var share = await _accountShareRepository.GetByIdAsync(request.ShareId);
        if (share == null)
            throw new ArgumentException($"Share with ID {request.ShareId} not found.");

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
