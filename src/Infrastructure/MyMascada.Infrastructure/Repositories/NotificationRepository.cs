using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Notification?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && !n.IsDeleted, cancellationToken);
    }

    public async Task<(List<Notification> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        NotificationType? type = null,
        bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId && !n.IsDeleted);

        if (type.HasValue)
            query = query.Where(n => n.Type == type.Value);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsDeleted, cancellationToken);
    }

    public async Task<Notification> CreateAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notification.CreatedAt = DateTime.UtcNow;
        notification.UpdatedAt = DateTime.UtcNow;

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public async Task MarkAsReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && !n.IsDeleted, cancellationToken);

        if (notification != null)
        {
            notification.MarkAsRead();
            notification.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now)
                .SetProperty(n => n.UpdatedAt, now),
                cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && !n.IsDeleted, cancellationToken);

        if (notification != null)
        {
            notification.IsDeleted = true;
            notification.DeletedAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteExpiredAsync(int retentionDays = 90, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var now = DateTime.UtcNow;
        await _context.Notifications
            .Where(n => n.CreatedAt < cutoff && !n.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsDeleted, true)
                .SetProperty(n => n.DeletedAt, now)
                .SetProperty(n => n.UpdatedAt, now),
                cancellationToken);
    }

    public async Task<bool> ExistsByGroupKeyAsync(Guid userId, string groupKey, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .AnyAsync(n => n.UserId == userId && n.GroupKey == groupKey && !n.IsDeleted, cancellationToken);
    }

    public async Task<int> CountRecentByTypeAsync(Guid userId, NotificationType type, TimeSpan window, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow - window;
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && n.Type == type && n.CreatedAt >= since && !n.IsDeleted, cancellationToken);
    }
}
