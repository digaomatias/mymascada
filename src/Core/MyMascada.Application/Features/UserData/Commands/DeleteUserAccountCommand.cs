using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.UserData.Commands;

/// <summary>
/// Command to permanently delete a user account and all associated data.
/// This implements the LGPD/GDPR "right to be forgotten".
/// </summary>
public class DeleteUserAccountCommand : IRequest<UserDeletionResultDto>
{
    public Guid UserId { get; set; }
}

public class DeleteUserAccountCommandHandler : IRequestHandler<DeleteUserAccountCommand, UserDeletionResultDto>
{
    private readonly IUserDataDeletionService _userDataDeletionService;

    public DeleteUserAccountCommandHandler(IUserDataDeletionService userDataDeletionService)
    {
        _userDataDeletionService = userDataDeletionService;
    }

    public async Task<UserDeletionResultDto> Handle(DeleteUserAccountCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(request.UserId));

        return await _userDataDeletionService.DeleteAllUserDataAsync(request.UserId, cancellationToken);
    }
}
