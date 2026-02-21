using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IUserFinancialProfileRepository
{
    Task<UserFinancialProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserFinancialProfile> CreateAsync(UserFinancialProfile profile, CancellationToken ct = default);
    Task<UserFinancialProfile> UpdateAsync(UserFinancialProfile profile, CancellationToken ct = default);
    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken ct = default);
}
