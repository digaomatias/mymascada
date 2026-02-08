using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.DTOs;

public class CreateAccountShareRequest
{
    public string Email { get; set; } = string.Empty;
    public AccountShareRole Role { get; set; }
}
