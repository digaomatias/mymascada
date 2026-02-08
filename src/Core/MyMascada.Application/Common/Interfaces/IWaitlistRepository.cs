using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface IWaitlistRepository
{
    Task AddAsync(WaitlistEntry entry);
    Task<WaitlistEntry?> GetByNormalizedEmailAsync(string normalizedEmail);
    Task<WaitlistEntry?> GetByIdAsync(Guid id);
    Task<(IReadOnlyList<WaitlistEntry> Items, int TotalCount)> GetPagedAsync(WaitlistStatus? status, int page, int pageSize);
    Task UpdateAsync(WaitlistEntry entry);
}
