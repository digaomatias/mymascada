namespace MyMascada.Application.Common.Interfaces;

public interface IDashboardNudgeDismissalRepository
{
    Task<IEnumerable<string>> GetActiveDismissedNudgeTypesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DismissNudgeAsync(Guid userId, string nudgeType, int snoozeDays, CancellationToken cancellationToken = default);
}
