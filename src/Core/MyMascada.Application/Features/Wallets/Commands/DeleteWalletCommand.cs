using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Wallets.Commands;

public class DeleteWalletCommand : IRequest<Unit>
{
    public int WalletId { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteWalletCommandHandler : IRequestHandler<DeleteWalletCommand, Unit>
{
    private readonly IWalletRepository _walletRepository;

    public DeleteWalletCommandHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<Unit> Handle(DeleteWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetWalletByIdAsync(request.WalletId, request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new ArgumentException("Wallet not found or you don't have permission to access it.");
        }

        await _walletRepository.DeleteWalletAsync(request.WalletId, request.UserId, cancellationToken);

        return Unit.Value;
    }
}
