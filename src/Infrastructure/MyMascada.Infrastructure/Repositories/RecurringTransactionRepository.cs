using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// Repository for user-created recurring transactions and their occurrences
/// </summary>
public class RecurringTransactionRepository : IRecurringTransactionRepository
{
    private readonly ApplicationDbContext _context;

    public RecurringTransactionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a recurring transaction by ID for a specific user
    /// </summary>
    public async Task<RecurringTransaction?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.RecurringTransactions
            .Include(r => r.Account)
            .Include(r => r.Category)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId && !r.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets all recurring transactions for a user
    /// </summary>
    public async Task<IEnumerable<RecurringTransaction>> GetByUserIdAsync(
        Guid userId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RecurringTransactions
            .Include(r => r.Account)
            .Include(r => r.Category)
            .Where(r => r.UserId == userId && !r.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.NextDueDate)
            .ThenBy(r => r.Description)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets active recurring transactions across all users that are due on or before the given date
    /// </summary>
    public async Task<IEnumerable<RecurringTransaction>> GetDueAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var endOfDay = asOfDate.Date.AddDays(1);

        return await _context.RecurringTransactions
            .Where(r => r.IsActive
                        && r.NextDueDate < endOfDay
                        && !r.IsDeleted)
            .OrderBy(r => r.UserId)
            .ThenBy(r => r.NextDueDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets active recurring transactions for a user
    /// </summary>
    public async Task<IEnumerable<RecurringTransaction>> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringTransactions
            .Include(r => r.Account)
            .Include(r => r.Category)
            .Where(r => r.UserId == userId && r.IsActive && !r.IsDeleted)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a new recurring transaction
    /// </summary>
    public async Task<RecurringTransaction> CreateAsync(
        RecurringTransaction recurringTransaction,
        CancellationToken cancellationToken = default)
    {
        var entry = await _context.RecurringTransactions.AddAsync(recurringTransaction, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Load navigation properties for mapping
        await _context.Entry(entry.Entity)
            .Reference(r => r.Account)
            .LoadAsync(cancellationToken);

        if (recurringTransaction.CategoryId.HasValue)
        {
            await _context.Entry(entry.Entity)
                .Reference(r => r.Category)
                .LoadAsync(cancellationToken);
        }

        return entry.Entity;
    }

    /// <summary>
    /// Updates an existing recurring transaction
    /// </summary>
    public async Task<RecurringTransaction> UpdateAsync(
        RecurringTransaction recurringTransaction,
        CancellationToken cancellationToken = default)
    {
        recurringTransaction.UpdatedAt = DateTime.UtcNow;
        _context.RecurringTransactions.Update(recurringTransaction);
        await _context.SaveChangesAsync(cancellationToken);
        return recurringTransaction;
    }

    /// <summary>
    /// Soft deletes a recurring transaction for a user
    /// </summary>
    public async Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var recurringTransaction = await _context.RecurringTransactions
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId && !r.IsDeleted, cancellationToken);

        if (recurringTransaction == null)
        {
            return false;
        }

        recurringTransaction.IsDeleted = true;
        recurringTransaction.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Checks whether an occurrence already exists for the given schedule and date
    /// </summary>
    public async Task<bool> HasOccurrenceAsync(
        int recurringTransactionId,
        DateTime scheduledDate,
        CancellationToken cancellationToken = default)
    {
        var date = scheduledDate.Date;
        return await _context.RecurringTransactionOccurrences
            .AnyAsync(o => o.RecurringTransactionId == recurringTransactionId
                           && o.ScheduledDate == date
                           && !o.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets the occurrence for the given schedule and date, or null when none exists
    /// </summary>
    public async Task<RecurringTransactionOccurrence?> GetOccurrenceAsync(
        int recurringTransactionId,
        DateTime scheduledDate,
        CancellationToken cancellationToken = default)
    {
        var date = scheduledDate.Date;
        return await _context.RecurringTransactionOccurrences
            .FirstOrDefaultAsync(o => o.RecurringTransactionId == recurringTransactionId
                                      && o.ScheduledDate == date
                                      && !o.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Creates a new occurrence record
    /// </summary>
    public async Task<RecurringTransactionOccurrence> CreateOccurrenceAsync(
        RecurringTransactionOccurrence occurrence,
        CancellationToken cancellationToken = default)
    {
        occurrence.ScheduledDate = occurrence.ScheduledDate.Date;
        var entry = await _context.RecurringTransactionOccurrences.AddAsync(occurrence, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    /// <summary>
    /// Attempts to insert an occurrence row, returning null when the unique
    /// (RecurringTransactionId, ScheduledDate) index rejects it
    /// </summary>
    public async Task<RecurringTransactionOccurrence?> TryCreateOccurrenceAsync(
        RecurringTransactionOccurrence occurrence,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await CreateOccurrenceAsync(occurrence, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Single-row insert into a table whose only constraint besides the PK is
            // the unique (RecurringTransactionId, ScheduledDate) index — another run
            // claimed this scheduled date first. Detach the failed entity so the
            // change tracker stays usable for subsequent schedules.
            _context.Entry(occurrence).State = EntityState.Detached;
            return null;
        }
    }

    /// <summary>
    /// Updates an existing occurrence record
    /// </summary>
    public async Task<RecurringTransactionOccurrence> UpdateOccurrenceAsync(
        RecurringTransactionOccurrence occurrence,
        CancellationToken cancellationToken = default)
    {
        occurrence.UpdatedAt = DateTime.UtcNow;
        _context.RecurringTransactionOccurrences.Update(occurrence);
        await _context.SaveChangesAsync(cancellationToken);
        return occurrence;
    }

    /// <summary>
    /// Gets the most recent occurrences for a recurring transaction
    /// </summary>
    public async Task<IEnumerable<RecurringTransactionOccurrence>> GetRecentOccurrencesAsync(
        int recurringTransactionId,
        Guid userId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringTransactionOccurrences
            .Where(o => o.RecurringTransactionId == recurringTransactionId
                        && o.RecurringTransaction.UserId == userId
                        && !o.IsDeleted)
            .OrderByDescending(o => o.ScheduledDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Checks whether the account still exists and is not soft-deleted
    /// (the Accounts query filter hides soft-deleted rows)
    /// </summary>
    public async Task<bool> AccountExistsAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await _context.Accounts.AnyAsync(a => a.Id == accountId, cancellationToken);
    }

    /// <summary>
    /// Detaches all tracked entities so a failed save for one schedule cannot
    /// poison the shared change tracker for subsequent schedules
    /// </summary>
    public void ResetChangeTracking()
    {
        _context.ChangeTracker.Clear();
    }
}
