using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.Notifications;

public class NotificationTriggerService : INotificationTriggerService
{
    private readonly INotificationService _notificationService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<NotificationTriggerService> _logger;

    public NotificationTriggerService(
        INotificationService notificationService,
        ITransactionRepository transactionRepository,
        ILogger<NotificationTriggerService> logger)
    {
        _notificationService = notificationService;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task CheckCategorizationReminderAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var uncategorized = await _transactionRepository.GetUncategorizedTransactionsAsync(userId, maxCount: 1, cancellationToken: cancellationToken);
            var uncategorizedList = uncategorized.ToList();

            if (uncategorizedList.Count == 0)
                return;

            // Get the actual full count for the notification message
            var allUncategorized = await _transactionRepository.GetUncategorizedTransactionsAsync(userId, cancellationToken: cancellationToken);
            var count = allUncategorized.Count();

            var groupKey = $"categorization-reminder-{DateTime.UtcNow:yyyy-MM-dd}";
            var data = JsonSerializer.Serialize(new { href = "/transactions/categorize", count });

            await _notificationService.CreateNotificationAsync(
                userId,
                NotificationType.CategorizationReminder,
                "Uncategorized Transactions",
                $"You have {count} uncategorized transaction{(count == 1 ? "" : "s")} waiting for review.",
                data,
                NotificationPriority.Normal,
                groupKey,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking categorization reminder for user {UserId}", userId);
        }
    }

    public async Task CheckRunwayWarningAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Placeholder: integrate with financial runway calculation service when available.
        // Would read runway data and send RunwayWarning or RunwayCritical based on user thresholds.
        _logger.LogDebug("Runway warning check for user {UserId} — not yet wired to runway calculator", userId);
        await Task.CompletedTask;
    }

    public async Task NotifyTransactionReminderAsync(
        Guid userId,
        string merchantName,
        decimal amount,
        DateTime dueDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupKey = $"transaction-reminder-{merchantName}-{dueDate:yyyy-MM-dd}";
            var data = JsonSerializer.Serialize(new { href = "/transactions" });

            await _notificationService.CreateNotificationAsync(
                userId,
                NotificationType.TransactionReminder,
                "Upcoming Transaction",
                $"{merchantName} — {amount:C} due on {dueDate:MMM dd}.",
                data,
                NotificationPriority.Normal,
                groupKey,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transaction reminder for user {UserId}", userId);
        }
    }
}
