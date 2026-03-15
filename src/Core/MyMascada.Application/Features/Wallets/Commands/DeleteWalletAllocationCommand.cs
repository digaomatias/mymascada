using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Wallets.Commands;

public class DeleteWalletAllocationCommand : IRequest<Unit>
{
    public int WalletId { get; set; }
    public int AllocationId { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteWalletAllocationCommandHandler : IRequestHandler<DeleteWalletAllocationCommand, Unit>
{
    private readonly IWalletRepository _walletRepository;

    public DeleteWalletAllocationCommandHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<Unit> Handle(DeleteWalletAllocationCommand request, CancellationToken cancellationToken)
    {
        // Verify wallet belongs to user
        var wallet = await _walletRepository.GetWalletByIdAsync(request.WalletId, request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new ArgumentException("Wallet not found or you don't have permission to access it.");
        }

        // Verify allocation exists and belongs to this wallet
        var allocation = await _walletRepository.GetAllocationByIdAsync(request.AllocationId, cancellationToken);
        if (allocation == null || allocation.WalletId != request.WalletId)
        {
            throw new ArgumentException("Allocation not found or does not belong to this wallet.");
        }

        await _walletRepository.DeleteAllocationAsync(request.AllocationId, cancellationToken);

        return Unit.Value;
    }
}
