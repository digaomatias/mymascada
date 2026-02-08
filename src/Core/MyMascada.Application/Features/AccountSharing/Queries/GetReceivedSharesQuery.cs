using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AccountSharing.DTOs;

namespace MyMascada.Application.Features.AccountSharing.Queries;

/// <summary>
/// Query to list all shares received by the current user.
/// </summary>
public class GetReceivedSharesQuery : IRequest<List<ReceivedShareDto>>
{
    public Guid UserId { get; set; }
}

public class GetReceivedSharesQueryHandler : IRequestHandler<GetReceivedSharesQuery, List<ReceivedShareDto>>
{
    private readonly IAccountShareRepository _accountShareRepository;

    public GetReceivedSharesQueryHandler(IAccountShareRepository accountShareRepository)
    {
        _accountShareRepository = accountShareRepository;
    }

    public async Task<List<ReceivedShareDto>> Handle(GetReceivedSharesQuery request, CancellationToken cancellationToken)
    {
        var shares = await _accountShareRepository.GetBySharedWithUserIdAsync(request.UserId);

        return shares.Select(s => new ReceivedShareDto
        {
            Id = s.Id,
            AccountId = s.AccountId,
            AccountName = s.Account?.Name ?? string.Empty,
            SharedByName = s.SharedByUser?.FullName ?? string.Empty,
            Role = s.Role,
            Status = s.Status,
            CreatedAt = s.CreatedAt
        }).ToList();
    }
}
