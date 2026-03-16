using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Wallets.Mappers;

public static class WalletMapper
{
    public static WalletDetailDto ToDetailDto(Wallet wallet, decimal balance)
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

    public static WalletSummaryDto ToSummaryDto(Wallet wallet, Dictionary<int, decimal> balances, int allocationCount)
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
            AllocationCount = allocationCount,
            CreatedAt = wallet.CreatedAt
        };
    }
}
