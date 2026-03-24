using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(ApplicationDbContext context, ILogger<NotificationRepository> logger)
    {
        _context = context;
        _logger = logger;
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
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
            || ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Race condition: another request inserted the same (UserId, GroupKey) concurrently.
            // Treat as a no-op — return the existing notification instead.
            _logger.LogDebug(
                "Duplicate notification suppressed for user {UserId} with groupKey {GroupKey} (concurrent insert)",
                notification.UserId, notification.GroupKey);

            _context.Entry(notification).State = EntityState.Detached;

            var existing = await _context.Notifications
                .FirstOrDefaultAsync(
                    n => n.UserId == notification.UserId && n.GroupKey == notification.GroupKey && !n.IsDeleted,
                    cancellationToken);

            return existing ?? notification;
        }

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

    public async Task<Notification?> CreateIfRateLimitNotExceededAsync(
        Notification notification,
        NotificationType type,
        TimeSpan rateLimitWindow,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        // Wrap count-check + insert in a serializable transaction to prevent races
        // where two concurrent requests both pass the count check and both insert.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var count = await CountRecentByTypeAsync(notification.UserId, type, rateLimitWindow, cancellationToken);
            if (count >= maxCount)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var created = await CreateAsync(notification, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return created;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
