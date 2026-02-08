using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.DTOs;

public class AccountShareDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid SharedWithUserId { get; set; }
    public string SharedWithUserEmail { get; set; } = string.Empty;
    public string SharedWithUserName { get; set; } = string.Empty;
    public AccountShareRole Role { get; set; }
    public AccountShareStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
