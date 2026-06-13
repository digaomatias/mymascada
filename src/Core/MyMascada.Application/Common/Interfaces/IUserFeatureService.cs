namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Resolves user-facing feature toggles for a given user. The effective value of
/// a feature is its per-user override (if any) falling back to the launch default,
/// AND-ed with the global capability (a feature can't be on for a user if the
/// server isn't configured for it).
/// </summary>
public interface IUserFeatureService
{
    /// <summary>Effective on/off for every known feature, ready to send to the client.</summary>
    Task<IReadOnlyDictionary<string, bool>> GetResolvedAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The raw admin view: each known feature with its launch default, the user's
    /// explicit override (null if none), and the resolved effective value.
    /// </summary>
    Task<IReadOnlyList<UserFeatureState>> GetAdminViewAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set or clear a per-user override. Passing <paramref name="enabled"/> = null
    /// removes the override so the feature falls back to its launch default.
    /// </summary>
    Task SetOverrideAsync(
        Guid userId, string featureKey, bool? enabled, CancellationToken cancellationToken = default);
}

/// <summary>Admin-facing per-feature state for a single user.</summary>
public record UserFeatureState(string Key, bool LaunchDefault, bool? Override, bool Effective);
