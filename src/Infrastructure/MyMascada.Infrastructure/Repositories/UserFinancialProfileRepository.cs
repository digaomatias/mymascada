using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class UserFinancialProfileRepository : IUserFinancialProfileRepository
{
    private readonly ApplicationDbContext _context;

    public UserFinancialProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserFinancialProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserFinancialProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && !p.IsDeleted, ct);
    }

    public async Task<UserFinancialProfile> CreateAsync(UserFinancialProfile profile, CancellationToken ct = default)
    {
        profile.CreatedAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        _context.UserFinancialProfiles.Add(profile);
        await _context.SaveChangesAsync(ct);

        return profile;
    }

    public async Task<UserFinancialProfile> UpdateAsync(UserFinancialProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTime.UtcNow;

        _context.UserFinancialProfiles.Update(profile);
        await _context.SaveChangesAsync(ct);

        return profile;
    }

    public async Task<bool> ExistsForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.UserFinancialProfiles
            .AnyAsync(p => p.UserId == userId && !p.IsDeleted, ct);
    }
}
