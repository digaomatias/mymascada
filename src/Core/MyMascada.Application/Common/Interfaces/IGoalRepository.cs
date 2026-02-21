using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IGoalRepository
{
    Task<IEnumerable<Goal>> GetGoalsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IEnumerable<Goal>> GetActiveGoalsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Goal?> GetGoalByIdAsync(int goalId, Guid userId, CancellationToken cancellationToken = default);

    Task<Goal> CreateGoalAsync(Goal goal, CancellationToken cancellationToken = default);

    Task<Goal> UpdateGoalAsync(Goal goal, CancellationToken cancellationToken = default);

    Task DeleteGoalAsync(int goalId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> GoalNameExistsAsync(Guid userId, string name, int? excludeId = null, CancellationToken cancellationToken = default);
}
