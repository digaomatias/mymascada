using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.Commands;

/// <summary>
/// Command for an account owner to change the role of an existing share.
/// </summary>
public class UpdateAccountShareRoleCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }
    public int AccountId { get; set; }
    public int ShareId { get; set; }
    public AccountShareRole NewRole { get; set; }
}

public class UpdateAccountShareRoleCommandHandler : IRequestHandler<UpdateAccountShareRoleCommand, Unit>
{
    private readonly IAccountShareRepository _accountShareRepository;
    private readonly IAccountAccessService _accountAccessService;

    public UpdateAccountShareRoleCommandHandler(
        IAccountShareRepository accountShareRepository,
        IAccountAccessService accountAccessService)
    {
        _accountShareRepository = accountShareRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<Unit> Handle(UpdateAccountShareRoleCommand request, CancellationToken cancellationToken)
    {
        // Validate the requesting user is the account owner
        var isOwner = await _accountAccessService.IsOwnerAsync(request.UserId, request.AccountId);
        if (!isOwner)
            throw new UnauthorizedAccessException("Only the account owner can update share roles.");

        // Find the share
        var share = await _accountShareRepository.GetByIdAsync(request.ShareId, request.AccountId);
        if (share == null)
            throw new ArgumentException($"Share with ID {request.ShareId} not found for account {request.AccountId}.");

        // Only active shares (Pending or Accepted) can have their role updated
        if (share.Status != AccountShareStatus.Pending && share.Status != AccountShareStatus.Accepted)
            throw new InvalidOperationException($"Cannot update role of a share with status '{share.Status}'.");

        // Update the role
        share.Role = request.NewRole;
        share.UpdatedAt = DateTimeProvider.UtcNow;

        await _accountShareRepository.UpdateAsync(share);

        return Unit.Value;
    }
}
