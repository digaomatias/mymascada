using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services.Auth;

/// <summary>
/// Server-side OAuth state store backed by PostgreSQL.
/// States are bound to a user ID, single-use, and expire after 10 minutes.
///
/// This was previously backed by IMemoryCache. That could not survive the OAuth
/// round-trip in production: the API app scales to zero on Fly, so while the user
/// was authenticating with their bank (no traffic hitting us for minutes) the
/// machine could idle down, and the callback would land on a process with no
/// stored state — rejecting a perfectly valid consent. Persisting the state also
/// makes the store correct across restarts, deploys, and multiple machines.
/// </summary>
public class OAuthStateStore : IOAuthStateStore
{
    private static readonly TimeSpan StateExpiry = TimeSpan.FromMinutes(10);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<OAuthStateStore> _logger;

    public OAuthStateStore(ApplicationDbContext context, ILogger<OAuthStateStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task StoreAsync(Guid userId, string state, CancellationToken cancellationToken = default)
    {
        var now = DateTimeProvider.UtcNow;

        // Drop expired states plus any state left over from an abandoned attempt by
        // this user. Filtered in the database; the table is single-row-per-user and
        // self-limiting, so the sweep stays cheap and we avoid a background job.
        var stale = await _context.OAuthStates
            .Where(s => s.ExpiresAt <= now || s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (stale.Count > 0)
        {
            _context.OAuthStates.RemoveRange(stale);
        }

        _context.OAuthStates.Add(new OAuthState
        {
            UserId = userId,
            State = state,
            ExpiresAt = now.Add(StateExpiry),
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Stored OAuth state for user {UserId}", userId);
    }

    public async Task<bool> ValidateAndConsumeAsync(Guid userId, string state, CancellationToken cancellationToken = default)
    {
        var stored = await _context.OAuthStates
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (stored == null)
        {
            _logger.LogWarning("OAuth state not found for user {UserId} — expired or never stored", userId);
            return false;
        }

        // Claiming the row IS the atomic step. This emits DELETE ... WHERE Id = @id;
        // if a concurrent callback already consumed it, zero rows are affected and EF
        // raises DbUpdateConcurrencyException — so exactly one caller can ever win.
        _context.OAuthStates.Remove(stored);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("OAuth state for user {UserId} was already consumed", userId);
            return false;
        }

        // Compare only AFTER consuming. The state is burned even on a mismatch, so a
        // wrong guess cannot be retried against the same stored value.
        if (!string.Equals(stored.State, state, StringComparison.Ordinal))
        {
            _logger.LogWarning("OAuth state mismatch for user {UserId}", userId);
            return false;
        }

        if (stored.ExpiresAt <= DateTimeProvider.UtcNow)
        {
            _logger.LogWarning("OAuth state expired for user {UserId}", userId);
            return false;
        }

        _logger.LogDebug("OAuth state validated and consumed for user {UserId}", userId);
        return true;
    }
}
