using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing recurring payment patterns
/// </summary>
public interface IRecurringPatternRepository
{
    // Pattern CRUD operations

    /// <summary>
    /// Gets a recurring pattern by ID for a specific user
    /// </summary>
    Task<RecurringPattern?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all recurring patterns for a user
    /// </summary>
    Task<IEnumerable<RecurringPattern>> GetByUserIdAsync(
        Guid userId,
        bool includeOccurrences = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recurring patterns by status for a user
    /// </summary>
    Task<IEnumerable<RecurringPattern>> GetByStatusAsync(
        Guid userId,
        RecurringPatternStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active recurring patterns for a user (Active or AtRisk)
    /// </summary>
    Task<IEnumerable<RecurringPattern>> GetActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a pattern by its normalized merchant key for a user
    /// </summary>
    Task<RecurringPattern?> GetByMerchantKeyAsync(
        Guid userId,
        string normalizedMerchantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns with expected dates in a date range for a user
    /// </summary>
    Task<IEnumerable<RecurringPattern>> GetUpcomingAsync(
        Guid userId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns where the grace window has expired (need status check)
    /// </summary>
    Task<IEnumerable<RecurringPattern>> GetPastDueAsync(
        Guid userId,
        DateTime currentDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new recurring pattern
    /// </summary>
    Task<RecurringPattern> CreateAsync(RecurringPattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing recurring pattern
    /// </summary>
    Task<RecurringPattern> UpdateAsync(RecurringPattern pattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a recurring pattern
    /// </summary>
    Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a pattern based on normalized merchant key (create or update)
    /// </summary>
    Task<RecurringPattern> UpsertAsync(RecurringPattern pattern, CancellationToken cancellationToken = default);

    // Occurrence operations

    /// <summary>
    /// Gets all occurrences for a pattern
    /// </summary>
    Task<IEnumerable<RecurringOccurrence>> GetOccurrencesAsync(
        int patternId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent occurrences for a pattern
    /// </summary>
    Task<IEnumerable<RecurringOccurrence>> GetRecentOccurrencesAsync(
        int patternId,
        Guid userId,
        int count = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new occurrence record
    /// </summary>
    Task<RecurringOccurrence> CreateOccurrenceAsync(
        RecurringOccurrence occurrence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an occurrence record
    /// </summary>
    Task<RecurringOccurrence> UpdateOccurrenceAsync(
        RecurringOccurrence occurrence,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a transaction is already linked to an occurrence
    /// </summary>
    Task<bool> IsTransactionLinkedAsync(
        int transactionId,
        CancellationToken cancellationToken = default);

    // Aggregation operations

    /// <summary>
    /// Gets the total monthly cost of all active patterns for a user
    /// </summary>
    Task<decimal> GetTotalMonthlyCostAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total annual cost of all active patterns for a user
    /// </summary>
    Task<decimal> GetTotalAnnualCostAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pattern statistics for a user
    /// </summary>
    Task<(int TotalPatterns, int ActivePatterns, int AtRiskPatterns, decimal TotalMonthlyCost)>
        GetStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets patterns by category for budget integration
    /// </summary>
    Task<IEnumerable<RecurringPattern>> GetByCategoryAsync(
        int categoryId,
        Guid userId,
        CancellationToken cancellationToken = default);

    // Bulk operations

    /// <summary>
    /// Gets all users who have transactions (for background job processing)
    /// </summary>
    Task<IEnumerable<Guid>> GetUserIdsWithTransactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk updates pattern statuses after processing
    /// </summary>
    Task BulkUpdateStatusAsync(
        IEnumerable<(int PatternId, RecurringPatternStatus NewStatus, int ConsecutiveMisses)> updates,
        CancellationToken cancellationToken = default);
}
