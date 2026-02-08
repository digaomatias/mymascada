using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

public class WaitlistEntry : BaseEntity<Guid>
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Locale { get; set; } = "en-US";
    public string Source { get; set; } = "landing-page";
    public string? IpAddress { get; set; }
    public WaitlistStatus Status { get; set; } = WaitlistStatus.Pending;
    public DateTime? InvitedAt { get; set; }
    public DateTime? RegisteredAt { get; set; }
}
