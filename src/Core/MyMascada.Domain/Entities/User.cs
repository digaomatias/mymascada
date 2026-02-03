using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class User : BaseEntity<Guid>
{
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString();
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
    
    public string? GoogleId { get; set; }
    
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}".Trim();
    
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; } = true;
    
    public string Currency { get; set; } = "USD";
    public string Locale { get; set; } = "en-US";
    public string TimeZone { get; set; } = "UTC";
    
    public string? ProfilePictureUrl { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<CategorizationRule> CategorizationRules { get; set; } = new List<CategorizationRule>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}