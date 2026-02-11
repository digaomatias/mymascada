using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AccountSharing.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.Commands;

/// <summary>
/// Command to accept a pending account share invitation by share ID (for in-app use).
/// </summary>
public class AcceptAccountShareByIdCommand : IRequest<AccountShareDto>
{
    public int ShareId { get; set; }
    public Guid UserId { get; set; }
}

public class AcceptAccountShareByIdCommandHandler : IRequestHandler<AcceptAccountShareByIdCommand, AccountShareDto>
{
    private readonly IAccountShareRepository _accountShareRepository;

    public AcceptAccountShareByIdCommandHandler(IAccountShareRepository accountShareRepository)
    {
        _accountShareRepository = accountShareRepository;
    }

    public async Task<AccountShareDto> Handle(AcceptAccountShareByIdCommand request, CancellationToken cancellationToken)
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
            throw new InvalidOperationException($"This invitation cannot be accepted because its status is '{share.Status}'.");

        // Accept the share
        share.Status = AccountShareStatus.Accepted;
        share.InvitationToken = null;
        share.UpdatedAt = DateTimeProvider.UtcNow;

        await _accountShareRepository.UpdateAsync(share);

        return new AccountShareDto
        {
            Id = share.Id,
            AccountId = share.AccountId,
            AccountName = share.Account.Name,
            SharedWithUserId = share.SharedWithUserId,
            SharedWithUserEmail = share.SharedWithUser.Email,
            SharedWithUserName = share.SharedWithUser.FullName,
            Role = share.Role,
            Status = share.Status,
            CreatedAt = share.CreatedAt
        };
    }
}
