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
    private readonly IMemoryCache _cache;
    private readonly ILogger<OAuthStateStore> _logger;

    public OAuthStateStore(IMemoryCache cache, ILogger<OAuthStateStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task StoreAsync(Guid userId, string state, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId);
        _cache.Set(key, state, StateExpiry);
        _logger.LogDebug("Stored OAuth state for user {UserId}", userId);
        return Task.CompletedTask;
    }

    public Task<bool> ValidateAndConsumeAsync(Guid userId, string state, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(userId);

        if (!_cache.TryGetValue(key, out string? storedState))
        {
            _logger.LogWarning("OAuth state not found for user {UserId} — expired or never stored", userId);
            return Task.FromResult(false);
        }

        // Remove immediately (single-use) regardless of match result
        _cache.Remove(key);

        if (!string.Equals(storedState, state, StringComparison.Ordinal))
        {
            _logger.LogWarning("OAuth state mismatch for user {UserId}", userId);
            return Task.FromResult(false);
        }

        _logger.LogDebug("OAuth state validated and consumed for user {UserId}", userId);
        return Task.FromResult(true);
    }

    private static string CacheKey(Guid userId) => $"oauth_state:{userId}";
}
