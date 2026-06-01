using System.Security.Cryptography;
using System.Text;
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
            var count = await _transactionRepository.CountUncategorizedTransactionsAsync(userId, cancellationToken);

            if (count == 0)
                return;

            var groupKey = $"categorization-reminder-{DateTime.UtcNow:yyyy-MM-dd}";
            var data = JsonSerializer.Serialize(new { href = "/transactions/categorize", count });

            // Use template keys for title/body so the client can render localised copy.
            // Raw values (count) are passed in the structured data payload.
            await _notificationService.CreateNotificationAsync(
                userId,
                NotificationType.CategorizationReminder,
                "CategorizationReminder",
                "CategorizationReminder.body",
                data,
                NotificationPriority.Normal,
                groupKey,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
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

    public async Task NotifyRuleSuggestionsAvailableAsync(
        Guid userId,
        int suggestionCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (suggestionCount <= 0) return;

            var groupKey = $"rule-suggestions-{DateTime.UtcNow:yyyy-MM-dd}";
            var data = JsonSerializer.Serialize(new
            {
                href = "/rules/suggestions",
                templateKey = "RuleSuggestionsAvailable",
                count = suggestionCount
            });

            await _notificationService.CreateNotificationAsync(
                userId,
                NotificationType.RuleSuggestionsAvailable,
                "RuleSuggestionsAvailable",
                "RuleSuggestionsAvailable.body",
                data,
                NotificationPriority.Normal,
                groupKey,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending rule suggestions notification for user {UserId}", userId);
        }
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
            // Hash the merchant name to guard against unbounded external strings exceeding the
            // 200-char GroupKey DB column limit.
            var merchantHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(merchantName.Trim().ToUpperInvariant())))[..16];
            var groupKey = $"transaction-reminder-{merchantHash}-{dueDate:yyyy-MM-dd}";

            // Store structured payload so the client can render localised copy.
            var data = JsonSerializer.Serialize(new
            {
                href = "/transactions",
                templateKey = "TransactionReminder",
                merchantName,
                amountMinorUnits = (long)Math.Round(amount * 100),
                dateIso = dueDate.ToString("yyyy-MM-dd")
            });

            // Use template keys for title/body so the client can render localised copy.
            // Structured raw values (merchantName, amountMinorUnits, dateIso) are in the data payload.
            await _notificationService.CreateNotificationAsync(
                userId,
                NotificationType.TransactionReminder,
                "TransactionReminder",
                "TransactionReminder.body",
                data,
                NotificationPriority.Normal,
                groupKey,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transaction reminder for user {UserId}", userId);
        }
    }
}
