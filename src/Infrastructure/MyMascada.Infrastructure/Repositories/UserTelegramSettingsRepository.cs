using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Common.Security;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class UserTelegramSettingsRepository : IUserTelegramSettingsRepository
{
    private readonly ApplicationDbContext _context;

    public UserTelegramSettingsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserTelegramSettings?> GetByUserIdAsync(Guid userId)
    {
        return await _context.UserTelegramSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);
    }

    public async Task<UserTelegramSettings?> GetByWebhookSecretAsync(string webhookSecret)
    {
        var hash = Sha256Hasher.Hash(webhookSecret);
        return await _context.UserTelegramSettings
            .FirstOrDefaultAsync(s => s.WebhookSecretHash == hash && s.IsActive);
    }

    public async Task<UserTelegramSettings> AddAsync(UserTelegramSettings settings)
    {
        await _context.UserTelegramSettings.AddAsync(settings);
        await _context.SaveChangesAsync();
        return settings;
    }

    public async Task UpdateAsync(UserTelegramSettings settings)
    {
        _context.UserTelegramSettings.Update(settings);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(UserTelegramSettings settings)
    {
        settings.IsDeleted = true;
        settings.DeletedAt = DateTimeProvider.UtcNow;
        settings.IsActive = false;
        _context.UserTelegramSettings.Update(settings);
        await _context.SaveChangesAsync();
    }
}
