using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Features;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services;

/// <summary>
/// Resolves per-user feature toggles: effective = (override ?? launch default)
/// AND the global capability for that feature.
/// </summary>
public class UserFeatureService : IUserFeatureService
{
    private readonly ApplicationDbContext _db;
    private readonly IFeatureFlags _global;

    public UserFeatureService(ApplicationDbContext db, IFeatureFlags global)
    {
        _db = db;
        _global = global;
    }

    /// <summary>
    /// Whether the server is configured for a feature at all. A feature can never
    /// be on for a user if the underlying capability isn't available.
    /// </summary>
    private bool GlobalCapability(string key) => key switch
    {
        UserFeatureKeys.AiAssistant => _global.HasGlobalAiKey,
        UserFeatureKeys.BankConnections => _global.BankSync,
        UserFeatureKeys.ReceiptScan => true, // on-device, always available
        _ => false,
    };

    public async Task<IReadOnlyDictionary<string, bool>> GetResolvedAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var overrides = await LoadOverridesAsync(userId, cancellationToken);
        var result = new Dictionary<string, bool>(UserFeatureKeys.All.Count);
        foreach (var (key, launchDefault) in UserFeatureKeys.LaunchDefaults)
        {
            var desired = overrides.TryGetValue(key, out var o) ? o : launchDefault;
            result[key] = desired && GlobalCapability(key);
        }
        return result;
    }

    public async Task<IReadOnlyList<UserFeatureState>> GetAdminViewAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var overrides = await LoadOverridesAsync(userId, cancellationToken);
        var states = new List<UserFeatureState>(UserFeatureKeys.All.Count);
        foreach (var (key, launchDefault) in UserFeatureKeys.LaunchDefaults)
        {
            bool? ov = overrides.TryGetValue(key, out var o) ? o : null;
            var desired = ov ?? launchDefault;
            states.Add(new UserFeatureState(key, launchDefault, ov, desired && GlobalCapability(key)));
        }
        return states;
    }

    public async Task SetOverrideAsync(
        Guid userId, string featureKey, bool? enabled, CancellationToken cancellationToken = default)
    {
        if (!UserFeatureKeys.LaunchDefaults.ContainsKey(featureKey))
        {
            throw new ArgumentException($"Unknown feature key '{featureKey}'.", nameof(featureKey));
        }

        var existing = await _db.UserFeatureFlags
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FeatureKey == featureKey, cancellationToken);

        if (enabled is null)
        {
            // Clear the override → fall back to the launch default.
            if (existing is not null)
            {
                _db.UserFeatureFlags.Remove(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        if (existing is null)
        {
            _db.UserFeatureFlags.Add(new UserFeatureFlag
            {
                UserId = userId,
                FeatureKey = featureKey,
                Enabled = enabled.Value,
            });
        }
        else
        {
            existing.Enabled = enabled.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, bool>> LoadOverridesAsync(
        Guid userId, CancellationToken cancellationToken) =>
        await _db.UserFeatureFlags
            .Where(f => f.UserId == userId)
            .ToDictionaryAsync(f => f.FeatureKey, f => f.Enabled, cancellationToken);
}
