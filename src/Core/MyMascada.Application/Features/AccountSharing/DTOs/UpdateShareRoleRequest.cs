using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.AccountSharing.DTOs;

public class UpdateShareRoleRequest
{
    public AccountShareRole Role { get; set; }
}
