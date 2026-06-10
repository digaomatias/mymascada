using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository for user-created recurring transactions (scheduled bills) and their occurrences
/// </summary>
public interface IRecurringTransactionRepository
{
    /// <summary>
    /// Gets a recurring transaction by ID for a specific user (includes Account/Category)
    /// </summary>
    Task<RecurringTransaction?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all recurring transactions for a user
    /// </summary>
    Task<IEnumerable<RecurringTransaction>> GetByUserIdAsync(
        Guid userId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active recurring transactions across all users that are due on or before the given date.
    /// Used by the daily background job.
    /// </summary>
    Task<IEnumerable<RecurringTransaction>> GetDueAsync(
        DateTime asOfDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active recurring transactions for a user (for upcoming schedule materialization)
    /// </summary>
    Task<IEnumerable<RecurringTransaction>> GetActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new recurring transaction
    /// </summary>
    Task<RecurringTransaction> CreateAsync(RecurringTransaction recurringTransaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing recurring transaction
    /// </summary>
    Task<RecurringTransaction> UpdateAsync(RecurringTransaction recurringTransaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a recurring transaction for a user. Returns true when found and deleted.
    /// </summary>
    Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an occurrence already exists for the given schedule and date
    /// (idempotency guard for the daily job)
    /// </summary>
    Task<bool> HasOccurrenceAsync(
        int recurringTransactionId,
        DateTime scheduledDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new occurrence record
    /// </summary>
    Task<RecurringTransactionOccurrence> CreateOccurrenceAsync(
        RecurringTransactionOccurrence occurrence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent occurrences for a recurring transaction
    /// </summary>
    Task<IEnumerable<RecurringTransactionOccurrence>> GetRecentOccurrencesAsync(
        int recurringTransactionId,
        Guid userId,
        int count = 10,
        CancellationToken cancellationToken = default);
}
