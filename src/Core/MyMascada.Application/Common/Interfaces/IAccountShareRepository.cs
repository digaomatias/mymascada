using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface IAccountShareRepository
{
    Task<AccountShare?> GetByIdAsync(int id);
    Task<AccountShare?> GetByIdAsync(int id, int accountId);
    Task<IEnumerable<AccountShare>> GetByAccountIdAsync(int accountId);
    Task<IEnumerable<AccountShare>> GetBySharedWithUserIdAsync(Guid userId);
    Task<IEnumerable<AccountShare>> GetAcceptedSharesForUserAsync(Guid userId);
    Task<AccountShare?> GetByInvitationTokenAsync(string tokenHash);
    Task<AccountShare?> GetActiveShareAsync(int accountId, Guid sharedWithUserId);
    Task<int> GetPendingCountForAccountAsync(int accountId);
    Task<AccountShare> AddAsync(AccountShare share);
    Task UpdateAsync(AccountShare share);
    Task DeleteAsync(AccountShare share);
    Task RevokeSharesByAccountIdAsync(int accountId);
    Task SaveChangesAsync();
}
