using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing Akahu user credentials.
/// </summary>
public class AkahuUserCredentialRepository : IAkahuUserCredentialRepository
{
    private readonly ApplicationDbContext _context;

    public AkahuUserCredentialRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<AkahuUserCredential?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.AkahuUserCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
    }

    /// <inheritdoc />
    public async Task<AkahuUserCredential?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.AkahuUserCredentials
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<bool> HasCredentialsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.AkahuUserCredentials
            .AnyAsync(c => c.UserId == userId, ct);
    }

    /// <inheritdoc />
    public async Task<AkahuUserCredential> AddAsync(AkahuUserCredential credential, CancellationToken ct = default)
    {
        await _context.AkahuUserCredentials.AddAsync(credential, ct);
        await _context.SaveChangesAsync(ct);
        return credential;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(AkahuUserCredential credential, CancellationToken ct = default)
    {
        _context.AkahuUserCredentials.Update(credential);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var credential = await _context.AkahuUserCredentials
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (credential != null)
        {
            // Soft delete following the pattern from other repositories
            credential.IsDeleted = true;
            credential.DeletedAt = DateTime.UtcNow;
            _context.AkahuUserCredentials.Update(credential);
            await _context.SaveChangesAsync(ct);
        }
    }
}
