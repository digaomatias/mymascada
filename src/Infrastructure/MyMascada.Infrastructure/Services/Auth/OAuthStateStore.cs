using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.Auth;

/// <summary>
/// Server-side OAuth state store backed by IMemoryCache.
/// States are bound to a user ID, single-use, and expire after 10 minutes.
/// </summary>
public class OAuthStateStore : IOAuthStateStore
{
    private static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(10);
    private readonly object _locksGate = new();
    private readonly Dictionary<string, LockEntry> _locks = new();
    private readonly IMemoryCache _cache;
    private readonly ILogger<OAuthStateStore> _logger;

    public OAuthStateStore(IMemoryCache cache, ILogger<OAuthStateStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task StoreAsync(Guid userId, string state, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId);
        var lockEntry = await AcquireLockAsync(key, cancellationToken);

        try
        {
            _cache.Set(key, state, StateExpiry);
            _logger.LogDebug("Stored OAuth state for user {UserId}", userId);
        }
        finally
        {
            ReleaseLock(key, lockEntry);
        }
    }

    public async Task<bool> ValidateAndConsumeAsync(Guid userId, string state, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId);
        var lockEntry = await AcquireLockAsync(key, cancellationToken);

        try
        {
            if (!_cache.TryGetValue(key, out string? storedState))
            {
                _logger.LogWarning("OAuth state not found for user {UserId} — expired or never stored", userId);
                return false;
            }

            // Remove immediately (single-use) regardless of match result
            _cache.Remove(key);

            if (!string.Equals(storedState, state, StringComparison.Ordinal))
            {
                _logger.LogWarning("OAuth state mismatch for user {UserId}", userId);
                return false;
            }

            _logger.LogDebug("OAuth state validated and consumed for user {UserId}", userId);
            return true;
        }
        finally
        {
            ReleaseLock(key, lockEntry);
        }
    }

    private async Task<LockEntry> AcquireLockAsync(string key, CancellationToken cancellationToken)
    {
        LockEntry lockEntry;

        lock (_locksGate)
        {
            if (!_locks.TryGetValue(key, out lockEntry!))
            {
                lockEntry = new LockEntry();
                _locks[key] = lockEntry;
            }

            lockEntry.RefCount++;
        }

        try
        {
            await lockEntry.Semaphore.WaitAsync(cancellationToken);
            return lockEntry;
        }
        catch
        {
            ReleaseLockReference(key, lockEntry);
            throw;
        }
    }

    private void ReleaseLock(string key, LockEntry lockEntry)
    {
        lockEntry.Semaphore.Release();
        ReleaseLockReference(key, lockEntry);
    }

    private void ReleaseLockReference(string key, LockEntry lockEntry)
    {
        var shouldDispose = false;

        lock (_locksGate)
        {
            lockEntry.RefCount--;

            if (lockEntry.RefCount == 0)
            {
                _locks.Remove(key);
                shouldDispose = true;
            }
        }

        if (shouldDispose)
        {
            lockEntry.Semaphore.Dispose();
        }
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount;
    }

    private static string CacheKey(Guid userId) => $"oauth_state:{userId}";
}
