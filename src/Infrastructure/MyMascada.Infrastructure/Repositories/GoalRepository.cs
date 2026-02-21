using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class GoalRepository : IGoalRepository
{
    private readonly ApplicationDbContext _context;

    public GoalRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Goal>> GetGoalsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Goals
            .Include(g => g.Account)
            .Where(g => g.UserId == userId && !g.IsDeleted)
            .OrderBy(g => g.DisplayOrder)
            .ThenByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Goal>> GetActiveGoalsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Goals
            .Include(g => g.Account)
            .Where(g => g.UserId == userId && g.Status == Domain.Enums.GoalStatus.Active && !g.IsDeleted)
            .OrderBy(g => g.DisplayOrder)
            .ThenByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Goal?> GetGoalByIdAsync(int goalId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Goals
            .Include(g => g.Account)
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId && !g.IsDeleted, cancellationToken);
    }

    public async Task<Goal> CreateGoalAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        goal.CreatedAt = DateTime.UtcNow;
        goal.UpdatedAt = DateTime.UtcNow;

        _context.Goals.Add(goal);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetGoalByIdAsync(goal.Id, goal.UserId, cancellationToken)
               ?? throw new InvalidOperationException("Failed to reload created goal");
    }

    public async Task<Goal> UpdateGoalAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        goal.UpdatedAt = DateTime.UtcNow;

        _context.Goals.Update(goal);
        await _context.SaveChangesAsync(cancellationToken);

        // Reload with includes
        return await GetGoalByIdAsync(goal.Id, goal.UserId, cancellationToken)
               ?? throw new InvalidOperationException("Failed to reload updated goal");
    }

    public async Task DeleteGoalAsync(int goalId, Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _context.Goals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId && !g.IsDeleted, cancellationToken);

        if (goal != null)
        {
            goal.IsDeleted = true;
            goal.DeletedAt = DateTime.UtcNow;
            goal.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> GoalNameExistsAsync(Guid userId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Goals
            .Where(g => g.UserId == userId && g.Name == name && !g.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(g => g.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
