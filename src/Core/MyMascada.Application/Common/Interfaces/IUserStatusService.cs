namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Computes user onboarding and AI configuration status flags.
/// Centralises the logic so all auth-related endpoints return consistent values.
/// </summary>
public interface IUserStatusService
{
    Task<(bool IsOnboardingComplete, bool HasAiConfigured)> GetStatusAsync(Guid userId);
}
