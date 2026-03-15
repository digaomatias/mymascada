using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Domain.Entities;

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

        return MapToDetailDto(wallet, balance);
    }

    private static WalletDetailDto MapToDetailDto(Wallet wallet, decimal balance)
    {
        return new WalletDetailDto
        {
            Id = wallet.Id,
            Name = wallet.Name,
            Icon = wallet.Icon,
            Color = wallet.Color,
            Currency = wallet.Currency,
            IsArchived = wallet.IsArchived,
            TargetAmount = wallet.TargetAmount,
            Balance = balance,
            AllocationCount = wallet.Allocations.Count(a => !a.IsDeleted),
            CreatedAt = wallet.CreatedAt,
            UpdatedAt = wallet.UpdatedAt,
            Allocations = wallet.Allocations
                .Where(a => !a.IsDeleted)
                .OrderByDescending(a => a.Transaction.TransactionDate)
                .ThenByDescending(a => a.CreatedAt)
                .Select(a => new WalletAllocationDto
                {
                    Id = a.Id,
                    TransactionId = a.TransactionId,
                    TransactionDescription = a.Transaction.GetDisplayDescription(),
                    TransactionDate = a.Transaction.TransactionDate,
                    AccountName = a.Transaction.Account?.Name ?? string.Empty,
                    Amount = a.Amount,
                    Note = a.Note,
                    CreatedAt = a.CreatedAt
                }).ToList()
        };
    }
}
