using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Domain.Entities;

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

        // Batch-load balances for all wallets
        var balances = await _walletRepository.GetWalletBalancesForUserAsync(request.UserId, cancellationToken);

        var walletSummaries = walletsList.Select(w => MapToSummaryDto(w, balances)).ToList();
        var totalBalance = walletSummaries.Sum(w => w.Balance);

        return new WalletDashboardSummaryDto
        {
            TotalBalance = totalBalance,
            Wallets = walletSummaries
        };
    }

    private static WalletSummaryDto MapToSummaryDto(Wallet wallet, Dictionary<int, decimal> balances)
    {
        return new WalletSummaryDto
        {
            Id = wallet.Id,
            Name = wallet.Name,
            Icon = wallet.Icon,
            Color = wallet.Color,
            Currency = wallet.Currency,
            IsArchived = wallet.IsArchived,
            TargetAmount = wallet.TargetAmount,
            Balance = balances.GetValueOrDefault(wallet.Id, 0m),
            AllocationCount = wallet.Allocations.Count(a => !a.IsDeleted),
            CreatedAt = wallet.CreatedAt
        };
    }
}
