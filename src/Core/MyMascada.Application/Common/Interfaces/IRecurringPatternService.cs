using MyMascada.Application.Features.UpcomingBills.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for detecting recurring payment patterns from transaction history
/// </summary>
public interface IRecurringPatternService
{
    /// <summary>
    /// Detects upcoming bills based on transaction history patterns
    /// </summary>
    /// <param name="userId">The user ID to analyze transactions for</param>
    /// <param name="daysAhead">Number of days ahead to look for upcoming bills</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected upcoming bills</returns>
    Task<UpcomingBillsResponse> GetUpcomingBillsAsync(
        Guid userId,
        int daysAhead = 7,
        CancellationToken cancellationToken = default);
}
