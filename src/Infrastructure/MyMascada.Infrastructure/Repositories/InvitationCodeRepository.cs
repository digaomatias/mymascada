using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class InvitationCodeRepository : IInvitationCodeRepository
{
    private readonly ApplicationDbContext _context;

    public InvitationCodeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(InvitationCode code)
    {
        await _context.InvitationCodes.AddAsync(code);
        await _context.SaveChangesAsync();
    }

    public async Task<InvitationCode?> GetByNormalizedCodeAsync(string normalizedCode)
    {
        return await _context.InvitationCodes
            .Include(c => c.WaitlistEntry)
            .FirstOrDefaultAsync(c => c.NormalizedCode == normalizedCode);
    }

    public async Task<InvitationCode?> GetByIdAsync(Guid id)
    {
        return await _context.InvitationCodes
            .Include(c => c.WaitlistEntry)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<(IReadOnlyList<InvitationCode> Items, int TotalCount)> GetPagedAsync(InvitationCodeStatus? status, int page, int pageSize)
    {
        var query = _context.InvitationCodes
            .Include(c => c.WaitlistEntry)
            .AsQueryable();
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task RevokeActiveCodesForEntryAsync(Guid waitlistEntryId)
    {
        var activeCodes = await _context.InvitationCodes
            .Where(c => c.WaitlistEntryId == waitlistEntryId && c.Status == InvitationCodeStatus.Active)
            .ToListAsync();

        foreach (var code in activeCodes)
        {
            code.Status = InvitationCodeStatus.Revoked;
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(InvitationCode code)
    {
        _context.InvitationCodes.Update(code);
        await _context.SaveChangesAsync();
    }
}
