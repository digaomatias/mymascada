using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationPreferenceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, cancellationToken);
    }

    public async Task<NotificationPreference> CreateOrUpdateAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
    {
        // Use IgnoreQueryFilters to also find soft-deleted rows — inserting when a soft-deleted
        // row exists would violate the unique index on UserId.
        var existing = await _context.NotificationPreferences
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == preference.UserId, cancellationToken);

        if (existing == null)
        {
            preference.CreatedAt = DateTime.UtcNow;
            preference.UpdatedAt = DateTime.UtcNow;
            _context.NotificationPreferences.Add(preference);
        }
        else
        {
            // Revive a soft-deleted row if needed
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.ChannelPreferences = preference.ChannelPreferences;
            existing.QuietHoursStart = preference.QuietHoursStart;
            existing.QuietHoursEnd = preference.QuietHoursEnd;
            existing.QuietHoursTimezone = preference.QuietHoursTimezone;
            existing.LargeTransactionThreshold = preference.LargeTransactionThreshold;
            existing.BudgetAlertPercentage = preference.BudgetAlertPercentage;
            existing.RunwayWarningMonths = preference.RunwayWarningMonths;
            existing.UpdatedAt = DateTime.UtcNow;
            preference = existing;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return preference;
    }
}
