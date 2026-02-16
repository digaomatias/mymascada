using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IUserTelegramSettingsRepository
{
    Task<UserTelegramSettings?> GetByUserIdAsync(Guid userId);
    Task<UserTelegramSettings?> GetByWebhookSecretAsync(string webhookSecret);
    Task<UserTelegramSettings> AddAsync(UserTelegramSettings settings);
    Task UpdateAsync(UserTelegramSettings settings);
    Task DeleteAsync(UserTelegramSettings settings);
}
