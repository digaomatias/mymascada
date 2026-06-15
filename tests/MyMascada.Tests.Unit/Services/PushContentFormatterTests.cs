using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Services.Push;

namespace MyMascada.Tests.Unit.Services;

public class PushContentFormatterTests
{
    [Fact]
    public void FormatContent_CategorizationReminderTemplate_ExpandsCount()
    {
        var (title, body) = PushContentFormatter.FormatContent(
            "CategorizationReminder", "CategorizationReminder.body",
            """{"href":"/transactions/categorize","count":5}""");

        title.Should().Be("Transactions to categorize");
        body.Should().Be("You have 5 uncategorized transactions");
    }

    [Fact]
    public void FormatContent_CategorizationReminderSingular_UsesSingularCopy()
    {
        var (_, body) = PushContentFormatter.FormatContent(
            "CategorizationReminder", "CategorizationReminder.body",
            """{"count":1}""");

        body.Should().Be("You have 1 uncategorized transaction");
    }

    [Fact]
    public void FormatContent_RuleSuggestionsTemplate_ExpandsCount()
    {
        var (title, body) = PushContentFormatter.FormatContent(
            "RuleSuggestionsAvailable", "RuleSuggestionsAvailable.body",
            """{"href":"/rules/suggestions","templateKey":"RuleSuggestionsAvailable","count":3}""");

        title.Should().Be("New rule suggestions");
        body.Should().Be("3 new categorization rule suggestions are available");
    }

    [Fact]
    public void FormatContent_TransactionReminderTemplate_ExpandsMerchantAmountAndDate()
    {
        var (title, body) = PushContentFormatter.FormatContent(
            "TransactionReminder", "TransactionReminder.body",
            """{"href":"/transactions","templateKey":"TransactionReminder","merchantName":"Netflix","amountMinorUnits":1599,"dateIso":"2026-06-15"}""");

        title.Should().Be("Upcoming transaction");
        body.Should().Be("Netflix — 15.99 due on 2026-06-15");
    }

    [Fact]
    public void FormatContent_NonTemplateNotification_PassesThroughUnchanged()
    {
        var (title, body) = PushContentFormatter.FormatContent(
            "Budget exceeded", "You spent more than your Groceries budget.", null);

        title.Should().Be("Budget exceeded");
        body.Should().Be("You spent more than your Groceries budget.");
    }

    [Fact]
    public void FormatContent_MalformedDataJson_FallsBackGracefully()
    {
        var (title, body) = PushContentFormatter.FormatContent(
            "CategorizationReminder", "CategorizationReminder.body", "{not-json");

        title.Should().Be("Transactions to categorize");
        body.Should().Be("You have 0 uncategorized transactions");
    }

    [Fact]
    public void BuildDataPayload_IncludesRouteNotificationIdAndType()
    {
        var notificationId = Guid.NewGuid();

        var payload = PushContentFormatter.BuildDataPayload(NotificationType.BudgetExceeded, notificationId);

        payload["route"].Should().Be("/dashboard/budgets");
        payload["notificationId"].Should().Be(notificationId.ToString());
        payload["notificationType"].Should().Be("BudgetExceeded");
    }

    [Theory]
    [InlineData(NotificationType.TransactionReminder, "/transactions")]
    [InlineData(NotificationType.CategorizationReminder, "/transactions")]
    [InlineData(NotificationType.LargeTransaction, "/transactions")]
    [InlineData(NotificationType.BudgetThreshold, "/dashboard/budgets")]
    [InlineData(NotificationType.BudgetExceeded, "/dashboard/budgets")]
    [InlineData(NotificationType.GoalMilestone, "/dashboard/goals")]
    [InlineData(NotificationType.AccountSyncFailed, "/dashboard/bank-connections")]
    [InlineData(NotificationType.RuleSuggestionsAvailable, "/dashboard/rules/suggestions")]
    [InlineData(NotificationType.SystemMessage, "/dashboard")]
    [InlineData(NotificationType.RunwayWarning, "/dashboard")]
    public void MapMobileRoute_MapsNotificationTypesToMobileRoutes(NotificationType type, string expectedRoute)
    {
        PushContentFormatter.MapMobileRoute(type).Should().Be(expectedRoute);
    }
}
