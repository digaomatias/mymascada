using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services.UserData;

/// <summary>
/// Service for permanently deleting all user data for LGPD/GDPR compliance (right to be forgotten).
/// </summary>
public class UserDataDeletionService : IUserDataDeletionService
{
    private readonly ApplicationDbContext _context;
    private readonly IAkahuApiClient _akahuApiClient;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly ILogger<UserDataDeletionService> _logger;

    public UserDataDeletionService(
        ApplicationDbContext context,
        IAkahuApiClient akahuApiClient,
        ISettingsEncryptionService encryptionService,
        ILogger<UserDataDeletionService> logger)
    {
        _context = context;
        _akahuApiClient = akahuApiClient;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<UserDeletionResultDto> DeleteAllUserDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var result = new UserDeletionResultDto
        {
            UserId = userId,
            DeletedAt = DateTime.UtcNow,
            Success = false
        };

        _logger.LogInformation("Starting complete data deletion for user {UserId} (LGPD/GDPR right to be forgotten)", userId);

        // Verify user exists
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for deletion", userId);
            result.ErrorMessage = $"User with ID {userId} not found";
            return result;
        }

        // Use a transaction for data integrity
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Batch 1: Fetch all IDs that only depend on userId (sequential - DbContext is not thread-safe)
            var accountIds = await _context.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.UserId == userId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            var categoryIds = await _context.Categories
                .IgnoreQueryFilters()
                .Where(c => c.UserId == userId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            var ruleIds = await _context.CategorizationRules
                .IgnoreQueryFilters()
                .Where(r => r.UserId == userId)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            var budgetIds = await _context.Budgets
                .IgnoreQueryFilters()
                .Where(b => b.UserId == userId)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);

            var walletIds = await _context.Wallets
                .IgnoreQueryFilters()
                .Where(w => w.UserId == userId)
                .Select(w => w.Id)
                .ToListAsync(cancellationToken);

            var recurringPatternIds = await _context.RecurringPatterns
                .IgnoreQueryFilters()
                .Where(rp => rp.UserId == userId)
                .Select(rp => rp.Id)
                .ToListAsync(cancellationToken);

            // Batch 2: Fetch IDs that depend on batch 1 results (sequential - DbContext is not thread-safe)
            var transactionIds = await _context.Transactions
                .IgnoreQueryFilters()
                .Where(t => accountIds.Contains(t.AccountId))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var reconciliationIds = await _context.Reconciliations
                .IgnoreQueryFilters()
                .Where(r => accountIds.Contains(r.AccountId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            var bankConnectionIds = await _context.BankConnections
                .IgnoreQueryFilters()
                .Where(bc => accountIds.Contains(bc.AccountId))
                .Select(bc => bc.Id)
                .ToListAsync(cancellationToken);

            var ruleSuggestionIds = await _context.RuleSuggestions
                .IgnoreQueryFilters()
                .Where(rs => categoryIds.Contains(rs.SuggestedCategoryId))
                .Select(rs => rs.Id)
                .ToListAsync(cancellationToken);

            // 1. Delete RuleSuggestionSamples
            if (ruleSuggestionIds.Any())
            {
                await _context.RuleSuggestionSamples
                    .IgnoreQueryFilters()
                    .Where(rss => ruleSuggestionIds.Contains(rss.RuleSuggestionId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 2. Delete RuleSuggestions
            if (ruleSuggestionIds.Any())
            {
                await _context.RuleSuggestions
                    .IgnoreQueryFilters()
                    .Where(rs => ruleSuggestionIds.Contains(rs.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 3. Delete RuleConditions
            if (ruleIds.Any())
            {
                await _context.RuleConditions
                    .IgnoreQueryFilters()
                    .Where(rc => ruleIds.Contains(rc.RuleId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 4. Delete RuleApplications
            if (ruleIds.Any())
            {
                await _context.Set<Domain.Entities.RuleApplication>()
                    .IgnoreQueryFilters()
                    .Where(ra => ruleIds.Contains(ra.RuleId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 5. Delete CategorizationCandidates
            if (transactionIds.Any())
            {
                await _context.CategorizationCandidates
                    .IgnoreQueryFilters()
                    .Where(cc => transactionIds.Contains(cc.TransactionId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 6. Delete TransactionSplits
            if (transactionIds.Any())
            {
                await _context.TransactionSplits
                    .IgnoreQueryFilters()
                    .Where(ts => transactionIds.Contains(ts.TransactionId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 7. Delete ReconciliationItems
            if (reconciliationIds.Any())
            {
                await _context.ReconciliationItems
                    .IgnoreQueryFilters()
                    .Where(ri => reconciliationIds.Contains(ri.ReconciliationId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 8. Delete ReconciliationAuditLogs
            if (reconciliationIds.Any())
            {
                await _context.ReconciliationAuditLogs
                    .IgnoreQueryFilters()
                    .Where(al => reconciliationIds.Contains(al.ReconciliationId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 9. Delete BankSyncLogs
            if (bankConnectionIds.Any())
            {
                await _context.BankSyncLogs
                    .IgnoreQueryFilters()
                    .Where(bsl => bankConnectionIds.Contains(bsl.BankConnectionId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 10. Delete BankConnections
            result.BankConnectionsDeleted = await _context.BankConnections
                .IgnoreQueryFilters()
                .Where(bc => bankConnectionIds.Contains(bc.Id))
                .ExecuteDeleteAsync(cancellationToken);

            // 11. Delete BankCategoryMappings
            await _context.BankCategoryMappings
                .IgnoreQueryFilters()
                .Where(bcm => bcm.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 12. Delete DuplicateExclusions
            await _context.DuplicateExclusions
                .IgnoreQueryFilters()
                .Where(de => de.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 13. Delete BudgetCategories
            if (budgetIds.Any())
            {
                await _context.BudgetCategories
                    .IgnoreQueryFilters()
                    .Where(bc => budgetIds.Contains(bc.BudgetId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 14. Delete Budgets
            await _context.Budgets
                .IgnoreQueryFilters()
                .Where(b => b.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 15. Delete WalletAllocations
            if (walletIds.Any())
            {
                await _context.WalletAllocations
                    .IgnoreQueryFilters()
                    .Where(wa => walletIds.Contains(wa.WalletId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 16. Delete Wallets
            await _context.Wallets
                .IgnoreQueryFilters()
                .Where(w => w.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 17. Delete RecurringOccurrences
            if (recurringPatternIds.Any())
            {
                await _context.RecurringOccurrences
                    .IgnoreQueryFilters()
                    .Where(ro => recurringPatternIds.Contains(ro.PatternId))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            // 18. Delete RecurringPatterns
            await _context.RecurringPatterns
                .IgnoreQueryFilters()
                .Where(rp => rp.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 19. Delete Goals
            await _context.Goals
                .IgnoreQueryFilters()
                .Where(g => g.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 20. Delete AccountShares
            await _context.AccountShares
                .IgnoreQueryFilters()
                .Where(ash => ash.SharedByUserId == userId || ash.SharedWithUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 21. Delete ChatMessages
            await _context.ChatMessages
                .IgnoreQueryFilters()
                .Where(cm => cm.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 22. Delete Notifications
            await _context.Notifications
                .IgnoreQueryFilters()
                .Where(n => n.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 23. Delete NotificationPreferences
            await _context.NotificationPreferences
                .IgnoreQueryFilters()
                .Where(np => np.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 24. Delete DashboardNudgeDismissals
            await _context.DashboardNudgeDismissals
                .IgnoreQueryFilters()
                .Where(dnd => dnd.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 25. Delete Transactions
            result.TransactionsDeleted = await _context.Transactions
                .IgnoreQueryFilters()
                .Where(t => transactionIds.Contains(t.Id))
                .ExecuteDeleteAsync(cancellationToken);

            // 26. Delete Transfers
            result.TransfersDeleted = await _context.Transfers
                .IgnoreQueryFilters()
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 27. Delete Reconciliations
            result.ReconciliationsDeleted = await _context.Reconciliations
                .IgnoreQueryFilters()
                .Where(r => reconciliationIds.Contains(r.Id))
                .ExecuteDeleteAsync(cancellationToken);

            // 28. Delete CategorizationRules
            result.RulesDeleted = await _context.CategorizationRules
                .IgnoreQueryFilters()
                .Where(r => r.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 29. Delete Categories
            result.CategoriesDeleted = await _context.Categories
                .IgnoreQueryFilters()
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 30. Delete Accounts
            result.AccountsDeleted = await _context.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 31. Revoke Akahu token before deleting credentials
            try
            {
                var credential = await _context.AkahuUserCredentials
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(auc => auc.UserId == userId, cancellationToken);

                if (credential != null)
                {
                    var appIdToken = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
                    var accessToken = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);

                    if (!string.IsNullOrEmpty(appIdToken) && !string.IsNullOrEmpty(accessToken))
                    {
                        _logger.LogDebug("Revoking Akahu access token for user {UserId} during account deletion", userId);
                        await _akahuApiClient.RevokeTokenAsync(appIdToken, accessToken, cancellationToken);
                        _logger.LogDebug("Successfully revoked Akahu access token for user {UserId}", userId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to revoke Akahu access token for user {UserId} during account deletion. Continuing with deletion.",
                    userId);
            }

            // 32. Delete AkahuUserCredentials
            await _context.AkahuUserCredentials
                .IgnoreQueryFilters()
                .Where(auc => auc.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 33. Delete RefreshTokens
            await _context.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 34. Delete PasswordResetTokens
            await _context.PasswordResetTokens
                .IgnoreQueryFilters()
                .Where(prt => prt.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 35. Delete EmailVerificationTokens
            await _context.EmailVerificationTokens
                .IgnoreQueryFilters()
                .Where(evt => evt.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 36. Delete UserAiSettings
            await _context.UserAiSettings
                .IgnoreQueryFilters()
                .Where(uas => uas.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 37. Delete UserTelegramSettings
            await _context.UserTelegramSettings
                .IgnoreQueryFilters()
                .Where(uts => uts.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 38. Delete UserFinancialProfiles
            await _context.UserFinancialProfiles
                .IgnoreQueryFilters()
                .Where(ufp => ufp.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 39. Delete AiTokenUsages
            await _context.AiTokenUsages
                .IgnoreQueryFilters()
                .Where(atu => atu.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 40. Delete UserSubscriptions
            await _context.UserSubscriptions
                .IgnoreQueryFilters()
                .Where(us => us.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 41. Delete User
            await _context.Users
                .IgnoreQueryFilters()
                .Where(u => u.Id == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            result.Success = true;

            _logger.LogInformation(
                "Data deletion completed for user {UserId}: {Accounts} accounts, {Transactions} transactions, {Categories} categories, {Rules} rules",
                userId, result.AccountsDeleted, result.TransactionsDeleted, result.CategoriesDeleted, result.RulesDeleted);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete data for user {UserId}", userId);
            await transaction.RollbackAsync(cancellationToken);
            result.ErrorMessage = $"Failed to delete user data: {ex.Message}";
            return result;
        }
    }
}
