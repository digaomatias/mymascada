namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for triggering notifications based on business events.
/// Call these methods from other services/handlers when conditions are met.
/// </summary>
public interface INotificationTriggerService
{
    /// <summary>
    /// Check for uncategorized transactions and send a batched reminder if threshold is met.
    /// </summary>
    Task CheckCategorizationReminderAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check financial runway and send warning/critical notifications.
    /// </summary>
    Task CheckRunwayWarningAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify about an upcoming scheduled transaction.
    /// </summary>
    Task NotifyTransactionReminderAsync(Guid userId, string merchantName, decimal amount, DateTime dueDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notify that new rule suggestions are available for the user.
    /// </summary>
    Task NotifyRuleSuggestionsAvailableAsync(Guid userId, int suggestionCount, CancellationToken cancellationToken = default);
}
