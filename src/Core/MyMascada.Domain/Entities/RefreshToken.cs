using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class RefreshToken : BaseEntity<Guid>
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
    public string CreatedByIp { get; set; } = string.Empty;
    
    // Foreign key to User
    public Guid UserId { get; set; }
    
    // Navigation property
    public User User { get; set; } = null!;
    
    // Computed properties
    public bool IsExpired => DateTime.UtcNow >= ExpiryDate;
    public bool IsActive => !IsRevoked && !IsExpired;
    
    public void Revoke(string ip, string? replacedByToken = null)
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
        RevokedByIp = ip;
        ReplacedByToken = replacedByToken;
    }
}