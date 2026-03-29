namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for permanently deleting all user data for LGPD/GDPR compliance (right to be forgotten).
/// </summary>
public interface IUserDataDeletionService
{
    /// <summary>
    /// Permanently deletes all data associated with a user account.
    /// This operation is irreversible.
    /// </summary>
    /// <param name="userId">The user ID to delete data for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of deleted data.</returns>
    Task<UserDeletionResultDto> DeleteAllUserDataAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a user data deletion operation.
/// </summary>
public class UserDeletionResultDto
{
    public Guid UserId { get; set; }
    public DateTime DeletedAt { get; set; }
    public int AccountsDeleted { get; set; }
    public int TransactionsDeleted { get; set; }
    public int CategoriesDeleted { get; set; }
    public int RulesDeleted { get; set; }
    public int TransfersDeleted { get; set; }
    public int ReconciliationsDeleted { get; set; }
    public int BankConnectionsDeleted { get; set; }
    public int BudgetsDeleted { get; set; }
    public int WalletsDeleted { get; set; }
    public int RecurringPatternsDeleted { get; set; }
    public int GoalsDeleted { get; set; }
    public int AccountSharesDeleted { get; set; }
    public int ChatMessagesDeleted { get; set; }
    public int NotificationsDeleted { get; set; }
    public int NotificationPreferencesDeleted { get; set; }
    public int DashboardNudgeDismissalsDeleted { get; set; }
    public int BankCategoryMappingsDeleted { get; set; }
    public int DuplicateExclusionsDeleted { get; set; }
    public int RuleSuggestionsDeleted { get; set; }
    public int EmailVerificationTokensDeleted { get; set; }
    public int RefreshTokensDeleted { get; set; }
    public int PasswordResetTokensDeleted { get; set; }
    public int AkahuUserCredentialsDeleted { get; set; }
    public int UserAiSettingsDeleted { get; set; }
    public int UserTelegramSettingsDeleted { get; set; }
    public int UserFinancialProfilesDeleted { get; set; }
    public int AiTokenUsagesDeleted { get; set; }
    public int UserSubscriptionsDeleted { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
