using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AccountSharing.DTOs;

namespace MyMascada.Application.Features.AccountSharing.Queries;

/// <summary>
/// Query to list all shares for an account (owner only).
/// </summary>
public class GetAccountSharesQuery : IRequest<List<AccountShareDto>>
{
    public Guid UserId { get; set; }
    public int AccountId { get; set; }
}

public class GetAccountSharesQueryHandler : IRequestHandler<GetAccountSharesQuery, List<AccountShareDto>>
{
    private readonly IAccountShareRepository _accountShareRepository;
    private readonly IAccountAccessService _accountAccessService;

    public GetAccountSharesQueryHandler(
        IAccountShareRepository accountShareRepository,
        IAccountAccessService accountAccessService)
    {
        _accountShareRepository = accountShareRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<List<AccountShareDto>> Handle(GetAccountSharesQuery request, CancellationToken cancellationToken)
    {
        // Validate the requesting user is the account owner
        var isOwner = await _accountAccessService.IsOwnerAsync(request.UserId, request.AccountId);
        if (!isOwner)
            throw new UnauthorizedAccessException("Only the account owner can view shares for this account.");

        var shares = await _accountShareRepository.GetByAccountIdAsync(request.AccountId);

        return shares.Select(s => new AccountShareDto
        {
            Id = s.Id,
            AccountId = s.AccountId,
            AccountName = s.Account?.Name ?? string.Empty,
            SharedWithUserId = s.SharedWithUserId,
            SharedWithUserEmail = s.SharedWithUser?.Email ?? string.Empty,
            SharedWithUserName = s.SharedWithUser?.FullName ?? string.Empty,
            Role = s.Role,
            Status = s.Status,
            CreatedAt = s.CreatedAt
        }).ToList();
    }
}
