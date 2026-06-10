using System.Globalization;
using System.Text.Json;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.Push;

/// <summary>
/// Builds push notification content and data payloads from in-app notifications.
///
/// In-app notifications created by <c>NotificationTriggerService</c> store i18n
/// template keys (e.g. title "CategorizationReminder", body
/// "CategorizationReminder.body") that web/mobile clients localise at render time.
/// Push notifications are rendered by the OS, so known template keys are expanded
/// to English copy here; non-template notifications pass through unchanged.
/// </summary>
public static class PushContentFormatter
{
    /// <summary>
    /// Resolves human-readable (English) title/body for a notification, expanding
    /// known template keys using values from the structured data payload.
    /// </summary>
    public static (string Title, string Body) FormatContent(string title, string body, string? dataJson)
    {
        var data = ParseData(dataJson);

        switch (title)
        {
            case "CategorizationReminder":
            {
                var count = GetInt(data, "count") ?? 0;
                return ("Transactions to categorize",
                    count == 1
                        ? "You have 1 uncategorized transaction"
                        : $"You have {count} uncategorized transactions");
            }
            case "RuleSuggestionsAvailable":
            {
                var count = GetInt(data, "count") ?? 0;
                return ("New rule suggestions",
                    count == 1
                        ? "1 new categorization rule suggestion is available"
                        : $"{count} new categorization rule suggestions are available");
            }
            case "TransactionReminder":
            {
                var merchant = GetString(data, "merchantName") ?? "Upcoming payment";
                var amountMinor = GetInt64(data, "amountMinorUnits");
                var date = GetString(data, "dateIso");
                var amountText = amountMinor.HasValue
                    ? (amountMinor.Value / 100m).ToString("0.00", CultureInfo.InvariantCulture)
                    : null;
                var bodyText = merchant;
                if (amountText != null) bodyText += $" — {amountText}";
                if (date != null) bodyText += $" due on {date}";
                return ("Upcoming transaction", bodyText);
            }
            default:
                return (title, body);
        }
    }

    /// <summary>
    /// Builds the FCM data payload the mobile app uses to deep-link on tap.
    /// The app's tap handler navigates using the "route" key (see
    /// notification_service.dart in mymascada-mobile).
    /// </summary>
    public static Dictionary<string, string> BuildDataPayload(NotificationType type, Guid notificationId)
    {
        return new Dictionary<string, string>
        {
            ["route"] = MapMobileRoute(type),
            ["notificationId"] = notificationId.ToString(),
            ["notificationType"] = type.ToString()
        };
    }

    /// <summary>
    /// Maps a notification type to a mobile app route (GoRouter path).
    /// Routes must exist in the mobile app's app_router.dart.
    /// </summary>
    public static string MapMobileRoute(NotificationType type) => type switch
    {
        NotificationType.TransactionReminder => "/transactions",
        NotificationType.RecurringTransactionCreated => "/transactions",
        NotificationType.CategorizationReminder => "/transactions",
        NotificationType.LargeTransaction => "/transactions",

        NotificationType.BudgetThreshold => "/dashboard/budgets",
        NotificationType.BudgetExceeded => "/dashboard/budgets",
        NotificationType.SpendingAnomaly => "/dashboard/budgets",

        NotificationType.GoalMilestone => "/dashboard/goals",
        NotificationType.GoalCompleted => "/dashboard/goals",
        NotificationType.GoalDeadlineApproaching => "/dashboard/goals",

        NotificationType.AccountSyncCompleted => "/dashboard/bank-connections",
        NotificationType.AccountSyncFailed => "/dashboard/bank-connections",
        NotificationType.AccountConnectionExpiring => "/dashboard/bank-connections",

        NotificationType.RuleSuggestionsAvailable => "/dashboard/rules/suggestions",

        _ => "/dashboard"
    };

    private static Dictionary<string, JsonElement>? ParseData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(Dictionary<string, JsonElement>? data, string key)
        => data != null && data.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static int? GetInt(Dictionary<string, JsonElement>? data, string key)
        => data != null && data.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v)
            ? v
            : null;

    private static long? GetInt64(Dictionary<string, JsonElement>? data, string key)
        => data != null && data.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var v)
            ? v
            : null;
}
