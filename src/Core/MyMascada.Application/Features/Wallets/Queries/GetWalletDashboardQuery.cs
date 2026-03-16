using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Application.Features.Wallets.Mappers;

namespace MyMascada.Application.Features.Wallets.Queries;

public class GetWalletDashboardQuery : IRequest<WalletDashboardSummaryDto>
{
    public Guid UserId { get; set; }
}

public class GetWalletDashboardQueryHandler : IRequestHandler<GetWalletDashboardQuery, WalletDashboardSummaryDto>
{
    private readonly IWalletRepository _walletRepository;

    public GetWalletDashboardQueryHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletDashboardSummaryDto> Handle(GetWalletDashboardQuery request, CancellationToken cancellationToken)
    {
        var wallets = await _walletRepository.GetWalletsForUserAsync(request.UserId, includeArchived: false, ct: cancellationToken);
        var walletsList = wallets.ToList();

        // Batch-load balances and allocation counts for all wallets
        var balances = await _walletRepository.GetWalletBalancesForUserAsync(request.UserId, cancellationToken);
        var allocationCounts = await _walletRepository.GetWalletAllocationCountsForUserAsync(request.UserId, cancellationToken);

        var walletSummaries = walletsList.Select(w => WalletMapper.ToSummaryDto(
            w, balances, allocationCounts.GetValueOrDefault(w.Id, 0))).ToList();
        var totalBalance = walletSummaries.Sum(w => w.Balance);

        // Group balances by currency for accurate per-currency totals
        var balanceByCurrency = walletSummaries
            .GroupBy(w => w.Currency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key.ToUpperInvariant(), g => g.Sum(w => w.Balance));

        return new WalletDashboardSummaryDto
        {
            TotalBalance = totalBalance,
            BalanceByCurrency = balanceByCurrency,
            Wallets = walletSummaries
        };
    }
}
