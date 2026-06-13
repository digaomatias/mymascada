using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

/// <summary>
/// A per-user override for a user-facing feature toggle (e.g. the AI assistant,
/// receipt scanning, bank connections). When no row exists for a (user, feature)
/// pair the feature falls back to its launch default. Managed by admins.
/// </summary>
public class UserFeatureFlag : BaseEntity<int>
{
    public Guid UserId { get; set; }

    /// <summary>Stable key from <see cref="MyMascada.Domain.Features.UserFeatureKeys"/>.</summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>Explicit per-user value overriding the launch default.</summary>
    public bool Enabled { get; set; }
}
