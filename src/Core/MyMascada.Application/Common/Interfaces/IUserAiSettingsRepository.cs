using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IUserAiSettingsRepository
{
    Task<UserAiSettings?> GetByUserIdAsync(Guid userId);
    Task<UserAiSettings> AddAsync(UserAiSettings settings);
    Task UpdateAsync(UserAiSettings settings);
    Task DeleteAsync(UserAiSettings settings);
    Task SaveChangesAsync();
}
