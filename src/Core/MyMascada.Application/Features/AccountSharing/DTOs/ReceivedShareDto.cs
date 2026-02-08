using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.DTOs;

public class ReceivedShareDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string SharedByName { get; set; } = string.Empty;
    public AccountShareRole Role { get; set; }
    public AccountShareStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
