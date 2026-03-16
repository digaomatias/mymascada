using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Application.Features.Wallets.Mappers;

namespace MyMascada.Application.Features.Wallets.Queries;

public class GetWalletQuery : IRequest<WalletDetailDto?>
{
    public int WalletId { get; set; }
    public Guid UserId { get; set; }
}

public class GetWalletQueryHandler : IRequestHandler<GetWalletQuery, WalletDetailDto?>
{
    private readonly IWalletRepository _walletRepository;

    public GetWalletQueryHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletDetailDto?> Handle(GetWalletQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetWalletByIdAsync(request.WalletId, request.UserId, cancellationToken);
        if (wallet == null)
        {
            return null;
        }

        var balance = await _walletRepository.GetWalletBalanceAsync(wallet.Id, cancellationToken);

        return WalletMapper.ToDetailDto(wallet, balance);
    }
}
