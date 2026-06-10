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
    /// Gets the occurrence for the given schedule and date, or null when none exists
    /// </summary>
    Task<RecurringTransactionOccurrence?> GetOccurrenceAsync(
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
    /// Attempts to insert an occurrence row, returning null when the unique
    /// (RecurringTransactionId, ScheduledDate) index rejects it because another
    /// run already claimed that scheduled date.
    /// </summary>
    Task<RecurringTransactionOccurrence?> TryCreateOccurrenceAsync(
        RecurringTransactionOccurrence occurrence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing occurrence record
    /// </summary>
    Task<RecurringTransactionOccurrence> UpdateOccurrenceAsync(
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

    /// <summary>
    /// Checks whether the account still exists and is not soft-deleted.
    /// Used by the daily job to deactivate schedules pointing at removed accounts.
    /// </summary>
    Task<bool> AccountExistsAsync(int accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches all tracked entities from the underlying unit of work. Called after
    /// a failed save so one schedule's poisoned change tracker cannot fail every
    /// subsequent schedule processed in the same scope.
    /// </summary>
    void ResetChangeTracking();
}
