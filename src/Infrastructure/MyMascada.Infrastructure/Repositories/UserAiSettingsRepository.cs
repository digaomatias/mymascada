using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class UserAiSettingsRepository : IUserAiSettingsRepository
{
    private readonly ApplicationDbContext _context;

    public UserAiSettingsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserAiSettings?> GetByUserIdAsync(Guid userId, string purpose = "general")
    {
        return await _context.UserAiSettings
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Purpose == purpose);
    }

    public async Task<UserAiSettings> AddAsync(UserAiSettings settings)
    {
        await _context.UserAiSettings.AddAsync(settings);
        await _context.SaveChangesAsync();
        return settings;
    }

    public async Task UpdateAsync(UserAiSettings settings)
    {
        _context.UserAiSettings.Update(settings);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(UserAiSettings settings)
    {
        settings.IsDeleted = true;
        settings.DeletedAt = DateTimeProvider.UtcNow;
        _context.UserAiSettings.Update(settings);
        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
