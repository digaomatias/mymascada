using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class DashboardNudgeDismissalRepository : IDashboardNudgeDismissalRepository
{
    private readonly ApplicationDbContext _context;

    public DashboardNudgeDismissalRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<string>> GetActiveDismissedNudgeTypesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.DashboardNudgeDismissals
            .Where(d => d.UserId == userId && d.SnoozedUntil > DateTime.UtcNow)
            .Select(d => d.NudgeType)
            .ToListAsync(cancellationToken);
    }

    public async Task DismissNudgeAsync(Guid userId, string nudgeType, int snoozeDays, CancellationToken cancellationToken = default)
    {
        var existing = await _context.DashboardNudgeDismissals
            .FirstOrDefaultAsync(d => d.UserId == userId && d.NudgeType == nudgeType, cancellationToken);

        if (existing != null)
        {
            existing.SnoozedUntil = DateTime.UtcNow.AddDays(snoozeDays);
        }
        else
        {
            _context.DashboardNudgeDismissals.Add(new DashboardNudgeDismissal
            {
                UserId = userId,
                NudgeType = nudgeType,
                SnoozedUntil = DateTime.UtcNow.AddDays(snoozeDays),
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
