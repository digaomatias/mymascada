using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Application.Features.Wallets.Mappers;

namespace MyMascada.Application.Features.Wallets.Queries;

public class GetWalletsQuery : IRequest<IEnumerable<WalletSummaryDto>>
{
    public Guid UserId { get; set; }
    public bool IncludeArchived { get; set; } = false;
}

public class GetWalletsQueryHandler : IRequestHandler<GetWalletsQuery, IEnumerable<WalletSummaryDto>>
{
    private readonly IWalletRepository _walletRepository;

    public GetWalletsQueryHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<IEnumerable<WalletSummaryDto>> Handle(GetWalletsQuery request, CancellationToken cancellationToken)
    {
        var wallets = await _walletRepository.GetWalletsForUserAsync(request.UserId, request.IncludeArchived, cancellationToken);
        var walletsList = wallets.ToList();

        // Batch-load balances and allocation counts for all wallets
        var balances = await _walletRepository.GetWalletBalancesForUserAsync(request.UserId, cancellationToken);
        var allocationCounts = await _walletRepository.GetWalletAllocationCountsForUserAsync(request.UserId, cancellationToken);

        return walletsList.Select(w => WalletMapper.ToSummaryDto(
            w, balances, allocationCounts.GetValueOrDefault(w.Id, 0))).ToList();
    }
}
