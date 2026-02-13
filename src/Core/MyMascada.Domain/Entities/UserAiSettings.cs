using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class UserAiSettings : BaseEntity<int>
{
    public Guid UserId { get; set; }
    public string Purpose { get; set; } = "general";
    public string ProviderType { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string? EncryptedApiKey { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string? ApiEndpoint { get; set; }
    public bool IsValidated { get; set; }
    public DateTime? LastValidatedAt { get; set; }
}
