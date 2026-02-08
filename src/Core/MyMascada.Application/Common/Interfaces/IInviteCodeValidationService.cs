namespace MyMascada.Application.Common.Interfaces;

public interface IInviteCodeValidationService
{
    Task<(bool IsValid, string? ErrorMessage)> ValidateAsync(string? code);
    Task<bool> ClaimAsync(string code, Guid userId);
}
