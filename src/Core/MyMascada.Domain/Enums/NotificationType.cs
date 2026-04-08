namespace MyMascada.Domain.Enums;

/// <summary>
/// Types of notifications that can be sent to users.
/// </summary>
public enum NotificationType
{
    // Transaction & Categorization
    TransactionReminder = 1,
    RecurringTransactionCreated = 2,
    CategorizationReminder = 3,
    LargeTransaction = 4,

    // Budget & Spending
    BudgetThreshold = 10,
    BudgetExceeded = 11,
    SpendingAnomaly = 12,

    // Goals
    GoalMilestone = 20,
    GoalCompleted = 21,
    GoalDeadlineApproaching = 22,

    // Financial Health
    RunwayWarning = 30,
    RunwayCritical = 31,
    NetWorthMilestone = 32,
    MonthlyReport = 33,

    // Accounts & Sync
    AccountSyncCompleted = 40,
    AccountSyncFailed = 41,
    AccountConnectionExpiring = 42,

    // AI & Insights
    AiInsight = 50,
    ReceiptProcessed = 51,
    ReceiptProcessingFailed = 52,
    RuleSuggestionsAvailable = 53,

    // System
    SystemMessage = 60,
    FeatureAnnouncement = 61,
    SecurityAlert = 62
}
