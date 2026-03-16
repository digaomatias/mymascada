using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IWalletRepository
{
    Task<IEnumerable<Wallet>> GetWalletsForUserAsync(Guid userId, bool includeArchived = false, CancellationToken ct = default);

    Task<Wallet?> GetWalletByIdAsync(int walletId, Guid userId, CancellationToken ct = default);

    Task<Wallet> CreateWalletAsync(Wallet wallet, CancellationToken ct = default);

    Task<Wallet> UpdateWalletAsync(Wallet wallet, CancellationToken ct = default);

    Task DeleteWalletAsync(int walletId, Guid userId, CancellationToken ct = default);

    Task<bool> WalletNameExistsAsync(Guid userId, string name, int? excludeId = null, CancellationToken ct = default);

    Task<WalletAllocation?> GetAllocationByIdAsync(int allocationId, CancellationToken ct = default);

    Task<WalletAllocation> CreateAllocationAsync(WalletAllocation allocation, CancellationToken ct = default);

    Task DeleteAllocationAsync(int allocationId, CancellationToken ct = default);

    Task<IEnumerable<WalletAllocation>> GetAllocationsForWalletAsync(int walletId, CancellationToken ct = default);

    Task<decimal> GetWalletBalanceAsync(int walletId, CancellationToken ct = default);

    Task<Dictionary<int, decimal>> GetWalletBalancesForUserAsync(Guid userId, CancellationToken ct = default);

    Task<Dictionary<int, int>> GetWalletAllocationCountsForUserAsync(Guid userId, CancellationToken ct = default);
}
