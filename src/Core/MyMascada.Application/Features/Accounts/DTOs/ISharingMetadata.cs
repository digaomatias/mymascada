using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Accounts.DTOs;

/// <summary>
/// Shared interface for DTOs that carry account sharing metadata.
/// Enables a single generic method to populate sharing fields.
/// </summary>
public interface ISharingMetadata
{
    int Id { get; }
    bool IsOwner { get; set; }
    bool IsSharedWithMe { get; set; }
    AccountShareRole? ShareRole { get; set; }
    string? SharedByUserName { get; set; }
}
