using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

public class InvitationCode : BaseEntity<Guid>
{
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public Guid? WaitlistEntryId { get; set; }
    public WaitlistEntry? WaitlistEntry { get; set; }
    public Guid? ClaimedByUserId { get; set; }
    public User? ClaimedByUser { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public InvitationCodeStatus Status { get; set; } = InvitationCodeStatus.Active;
    public int MaxUses { get; set; } = 1;
    public int UseCount { get; set; } = 0;
}
