using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// EF Core repository implementation for <see cref="AkahuWebhookSubscription"/>.
/// </summary>
public class AkahuWebhookSubscriptionRepository : IAkahuWebhookSubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    public AkahuWebhookSubscriptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AkahuWebhookSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.AkahuWebhookSubscriptions
            .Where(s => s.UserId == userId && !s.IsDeleted)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<AkahuWebhookSubscription?> GetByWebhookIdAsync(string webhookId, CancellationToken ct = default)
    {
        return await _context.AkahuWebhookSubscriptions
            .FirstOrDefaultAsync(s => s.WebhookId == webhookId && !s.IsDeleted, ct);
    }

    /// <inheritdoc />
    public async Task<AkahuWebhookSubscription> AddAsync(AkahuWebhookSubscription subscription, CancellationToken ct = default)
    {
        await _context.AkahuWebhookSubscriptions.AddAsync(subscription, ct);
        await _context.SaveChangesAsync(ct);
        return subscription;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(AkahuWebhookSubscription subscription, CancellationToken ct = default)
    {
        _context.AkahuWebhookSubscriptions.Update(subscription);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteByIdAsync(int id, CancellationToken ct = default)
    {
        var subscription = await _context.AkahuWebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (subscription != null)
        {
            subscription.IsDeleted = true;
            subscription.DeletedAt = DateTime.UtcNow;
            _context.AkahuWebhookSubscriptions.Update(subscription);
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var subscriptions = await _context.AkahuWebhookSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        if (subscriptions.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var subscription in subscriptions)
        {
            subscription.IsDeleted = true;
            subscription.DeletedAt = now;
        }

        _context.AkahuWebhookSubscriptions.UpdateRange(subscriptions);
        await _context.SaveChangesAsync(ct);
    }
}
