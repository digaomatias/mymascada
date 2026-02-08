using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.Commands;

/// <summary>
/// Command for an account owner to revoke a share.
/// </summary>
public class RevokeAccountShareCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
    public int AccountId { get; set; }
    public int ShareId { get; set; }
}

public class RevokeAccountShareCommandHandler : IRequestHandler<RevokeAccountShareCommand, Unit>
{
    private readonly IAccountShareRepository _accountShareRepository;
    private readonly IAccountAccessService _accountAccessService;

    public RevokeAccountShareCommandHandler(
        IAccountShareRepository accountShareRepository,
        IAccountAccessService accountAccessService)
    {
        _accountShareRepository = accountShareRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<Unit> Handle(RevokeAccountShareCommand request, CancellationToken cancellationToken)
    {
        // Validate the requesting user is the account owner
        var isOwner = await _accountAccessService.IsOwnerAsync(request.UserId, request.AccountId);
        if (!isOwner)
            throw new UnauthorizedAccessException("Only the account owner can revoke shares.");

        // Find the share
        var share = await _accountShareRepository.GetByIdAsync(request.ShareId, request.AccountId);
        if (share == null)
            throw new ArgumentException($"Share with ID {request.ShareId} not found for account {request.AccountId}.");

        // Only Pending or Accepted shares can be revoked
        if (share.Status != AccountShareStatus.Pending && share.Status != AccountShareStatus.Accepted)
            throw new InvalidOperationException($"Cannot revoke a share with status '{share.Status}'.");

        // Revoke the share
        share.Status = AccountShareStatus.Revoked;
        share.InvitationToken = null;
        share.UpdatedAt = DateTimeProvider.UtcNow;

        await _accountShareRepository.UpdateAsync(share);

        return Unit.Value;
    }
}
