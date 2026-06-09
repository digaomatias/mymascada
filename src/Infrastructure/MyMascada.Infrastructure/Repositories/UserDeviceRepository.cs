using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class UserDeviceRepository : IUserDeviceRepository
{
    private readonly ApplicationDbContext _context;

    public UserDeviceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserDevice> UpsertAsync(Guid userId, string fcmToken, string platform, CancellationToken cancellationToken = default)
    {
        // FcmToken has a unique index; use IgnoreQueryFilters so a soft-deleted row
        // does not block re-registration of the same token.
        var existing = await _context.UserDevices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.FcmToken == fcmToken, cancellationToken);

        if (existing == null)
        {
            var device = new UserDevice
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FcmToken = fcmToken,
                Platform = platform,
                LastSeenAt = DateTime.UtcNow
            };
            _context.UserDevices.Add(device);
            await _context.SaveChangesAsync(cancellationToken);
            return device;
        }

        // Reassign if a different user logged in on the same device, revive soft-deleted rows.
        existing.UserId = userId;
        existing.Platform = platform;
        existing.LastSeenAt = DateTime.UtcNow;
        existing.IsDeleted = false;
        existing.DeletedAt = null;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeleteByTokenAsync(Guid userId, string fcmToken, CancellationToken cancellationToken = default)
    {
        var device = await _context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.FcmToken == fcmToken, cancellationToken);

        if (device == null)
            return;

        device.IsDeleted = true;
        device.DeletedAt = DateTime.UtcNow;
        device.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<UserDevice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserDevices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveTokensAsync(IReadOnlyCollection<string> fcmTokens, CancellationToken cancellationToken = default)
    {
        if (fcmTokens.Count == 0)
            return;

        var devices = await _context.UserDevices
            .Where(d => fcmTokens.Contains(d.FcmToken))
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var device in devices)
        {
            device.IsDeleted = true;
            device.DeletedAt = now;
            device.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
