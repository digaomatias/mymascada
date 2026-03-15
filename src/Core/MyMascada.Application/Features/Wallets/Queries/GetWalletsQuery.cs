using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Domain.Entities;

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

        // Batch-load balances for all wallets
        var balances = await _walletRepository.GetWalletBalancesForUserAsync(request.UserId, cancellationToken);

        return walletsList.Select(w => MapToSummaryDto(w, balances)).ToList();
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
