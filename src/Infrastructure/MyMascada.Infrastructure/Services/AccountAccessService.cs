using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services;

/// <summary>
/// Central authorization service for account access.
/// Scoped lifetime: caches accessible account IDs per-request to avoid repeated DB hits.
/// Phase 0: Only returns owned accounts (behavior-preserving).
/// Phase 1: When AccountSharing feature flag is ON, also includes accepted shares.
/// </summary>
public class AccountAccessService : IAccountAccessService
{
    private readonly ApplicationDbContext _context;
    private readonly IFeatureFlags _featureFlags;

    // Per-request cache keyed by userId
    private readonly Dictionary<Guid, IReadOnlySet<int>> _accessCache = new();
    private readonly Dictionary<Guid, HashSet<int>> _ownedCache = new();

    public AccountAccessService(ApplicationDbContext context, IFeatureFlags featureFlags)
    {
        _context = context;
        _featureFlags = featureFlags;
    }

    public async Task<IReadOnlySet<int>> GetAccessibleAccountIdsAsync(Guid userId)
    {
        if (_accessCache.TryGetValue(userId, out var cached))
            return cached;

        var ownedIds = await GetOwnedAccountIdsAsync(userId);
        var result = new HashSet<int>(ownedIds);

        if (_featureFlags.AccountSharing)
        {
            var sharedIds = await _context.AccountShares
                .Where(s => s.SharedWithUserId == userId && s.Status == AccountShareStatus.Accepted)
                .Select(s => s.AccountId)
                .ToListAsync();

            result.UnionWith(sharedIds);
        }

        _accessCache[userId] = result;
        return result;
    }

    public async Task<bool> CanAccessAccountAsync(Guid userId, int accountId)
    {
        var accessible = await GetAccessibleAccountIdsAsync(userId);
        return accessible.Contains(accountId);
    }

    public async Task<bool> CanModifyAccountAsync(Guid userId, int accountId)
    {
        // Owner can always modify
        if (await IsOwnerAsync(userId, accountId))
            return true;

        if (!_featureFlags.AccountSharing)
            return false;

        // Manager role can modify
        return await _context.AccountShares
            .AnyAsync(s =>
                s.AccountId == accountId &&
                s.SharedWithUserId == userId &&
                s.Status == AccountShareStatus.Accepted &&
                s.Role == AccountShareRole.Manager);
    }

    public async Task<bool> IsOwnerAsync(Guid userId, int accountId)
    {
        var ownedIds = await GetOwnedAccountIdsAsync(userId);
        return ownedIds.Contains(accountId);
    }

    public async Task<Guid?> GetAccountOwnerIdAsync(int accountId)
    {
        return await _context.Accounts
            .Where(a => a.Id == accountId)
            .Select(a => (Guid?)a.UserId)
            .FirstOrDefaultAsync();
    }

    private async Task<HashSet<int>> GetOwnedAccountIdsAsync(Guid userId)
    {
        if (_ownedCache.TryGetValue(userId, out var cached))
            return cached;

        var ids = await _context.Accounts
            .Where(a => a.UserId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        var set = new HashSet<int>(ids);
        _ownedCache[userId] = set;
        return set;
    }
}
