using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.Notifications;

public class NotificationTriggerService : INotificationTriggerService
{
    /// <summary>
    /// Threshold (percent of effective budget) at which an "approaching limit"
    /// alert fires when the user has not set their own preference. Matches
    /// <c>BudgetCategory.IsApproachingLimit</c>'s hardcoded 80%.
    /// </summary>
    private const int DefaultBudgetAlertThresholdPercent = 80;

    /// <summary>
    /// Upper bound for the percentage reported in a budget alert. Guards against
    /// an int overflow when an effective budget is tiny relative to spend; any
    /// value at or above this just reads as "999999%+".
    /// </summary>
    private const int MaxReportedUsedPercentage = 999999;

    private readonly INotificationService _notificationService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _budgetCalculationService;
    private readonly INotificationPreferenceRepository _notificationPreferenceRepository;
    private readonly ILogger<NotificationTriggerService> _logger;

    public NotificationTriggerService(
        INotificationService notificationService,
        ITransactionRepository transactionRepository,
        IBudgetRepository budgetRepository,
        IBudgetCalculationService budgetCalculationService,
        INotificationPreferenceRepository notificationPreferenceRepository,
        ILogger<NotificationTriggerService> logger)
    {
        _notificationService = notificationService;
        _transactionRepository = transactionRepository;
        _budgetRepository = budgetRepository;
        _budgetCalculationService = budgetCalculationService;
        _notificationPreferenceRepository = notificationPreferenceRepository;
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

    public async Task CheckBudgetThresholdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Deliberately does NOT swallow exceptions: the caller is
        // BudgetAlertJobService, which isolates failures per user and aggregates
        // them so Hangfire records a failed run and retries. Swallowing here
        // would make a transient DB/notification failure silently skip a user's
        // alerts until the next daily run.
        var budgets = await _budgetRepository.GetActiveBudgetsForUserAsync(userId, cancellationToken);
        if (budgets is null)
            return;

        // The same preference row that controls quiet hours / channels also
        // stores the per-user alert threshold. Null means "use the default".
        var preference = await _notificationPreferenceRepository.GetByUserIdAsync(userId, cancellationToken);
        var alertThreshold = preference?.BudgetAlertPercentage ?? DefaultBudgetAlertThresholdPercent;

        foreach (var budget in budgets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Reuse the same calculation the budget detail page uses, so the
            // alert and the UI can never disagree about a category's status.
            var progress = await _budgetCalculationService.CalculateBudgetProgressAsync(
                budget, userId, cancellationToken);

            foreach (var category in progress.Categories)
            {
                // Skip only genuinely-unbudgeted categories: no allocation AND no
                // rollover carry. Their effective budget is zero, which makes
                // UsedPercentage meaningless (always >= 100%) and would spam users
                // about categories they never budgeted for.
                //
                // A category with a real allocation whose EffectiveBudget is <= 0
                // because a negative rollover (carry-overspend) wiped it out is
                // legitimately over budget — it must NOT be skipped, otherwise a
                // user who starts the period already in rollover debt never gets
                // the exceeded alert.
                if (category.BudgetedAmount <= 0 && category.RolloverAmount == 0)
                    continue;

                if (category.IsOverBudget)
                {
                    // Exceeded takes precedence over approaching — only the most
                    // severe alert fires for a given category.
                    await CreateBudgetThresholdNotificationAsync(
                        userId, budget.Id, budget.StartDate, category,
                        NotificationType.BudgetExceeded, "exceeded",
                        NotificationPriority.High, cancellationToken);
                }
                else if (category.UsedPercentage >= alertThreshold)
                {
                    await CreateBudgetThresholdNotificationAsync(
                        userId, budget.Id, budget.StartDate, category,
                        NotificationType.BudgetThreshold, "approaching",
                        NotificationPriority.Normal, cancellationToken);
                }
            }
        }
    }

    private async Task CreateBudgetThresholdNotificationAsync(
        Guid userId,
        int budgetId,
        DateTime periodStart,
        BudgetCategoryProgressDto category,
        NotificationType type,
        string conditionTag,
        NotificationPriority priority,
        CancellationToken cancellationToken)
    {
        // Scope the idempotency key to the budget PERIOD, not the run date. This
        // guarantees exactly one "approaching" and one "exceeded" notification per
        // category per period, no matter how often the daily job runs. The key
        // resets automatically when the next period starts (new StartDate).
        var groupKey =
            $"budget:{budgetId}:cat:{category.CategoryId}:threshold:{conditionTag}:{periodStart:yyyy-MM-dd}";

        // Cap before casting to int: a tiny effective budget (e.g. $0.01) with
        // large spend produces an enormous percentage that could overflow int.
        var usedPercentage = category.UsedPercentage >= MaxReportedUsedPercentage
            ? MaxReportedUsedPercentage
            : (int)Math.Round(category.UsedPercentage);

        // For an over-budget category whose effective budget is zero or negative
        // (rollover debt), GetUsedPercentage returns 0 or a negative value, which
        // would render as "over budget — -33% spent". Floor an exceeded alert at
        // 100% so the reported percentage is never a nonsensical sub-100 figure.
        if (category.IsOverBudget && usedPercentage < 100)
            usedPercentage = 100;
        var data = JsonSerializer.Serialize(new
        {
            href = $"/budgets/{budgetId}",
            templateKey = type.ToString(),
            budgetId,
            categoryId = category.CategoryId,
            categoryName = category.CategoryName,
            usedPercentage,
            // Minor units for future push/email channels that need to render currency.
            budgetedAmountMinorUnits = (long)Math.Round(category.EffectiveBudget * 100),
            actualSpentMinorUnits = (long)Math.Round(category.ActualSpent * 100)
        });

        // Title/body are template keys the client resolves to localised copy;
        // the raw values for interpolation travel in the data payload.
        //
        // periodDeduplicated: budget alerts fan out one per category and are bounded
        // by the period-scoped groupKey. This skips the per-type daily cap (which
        // would drop alerts for users with many categories) and keeps the alert
        // suppressed for the period even if the user deletes it from the bell.
        await _notificationService.CreateNotificationAsync(
            userId,
            type,
            type.ToString(),
            $"{type}.body",
            data,
            priority,
            groupKey,
            periodDeduplicated: true,
            cancellationToken: cancellationToken);
    }
}
