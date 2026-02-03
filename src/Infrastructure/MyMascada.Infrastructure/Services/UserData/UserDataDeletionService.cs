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
    private readonly ILogger<UserDataDeletionService> _logger;

    public UserDataDeletionService(
        ApplicationDbContext context,
        ILogger<UserDataDeletionService> logger)
    {
        _context = context;
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
            // Get all user's accounts first (needed for related queries)
            var accountIds = await _context.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.UserId == userId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            // Get all user's categories (needed for related queries)
            var categoryIds = await _context.Categories
                .IgnoreQueryFilters()
                .Where(c => c.UserId == userId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            // Get all user's rules
            var ruleIds = await _context.CategorizationRules
                .IgnoreQueryFilters()
                .Where(r => r.UserId == userId)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            // Get all transactions for these accounts
            var transactionIds = await _context.Transactions
                .IgnoreQueryFilters()
                .Where(t => accountIds.Contains(t.AccountId))
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            // Get all reconciliations
            var reconciliationIds = await _context.Reconciliations
                .IgnoreQueryFilters()
                .Where(r => accountIds.Contains(r.AccountId))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            // Get all bank connections
            var bankConnectionIds = await _context.BankConnections
                .IgnoreQueryFilters()
                .Where(bc => accountIds.Contains(bc.AccountId))
                .Select(bc => bc.Id)
                .ToListAsync(cancellationToken);

            // Get all rule suggestions for user's categories
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

            // 13. Delete Transactions
            result.TransactionsDeleted = await _context.Transactions
                .IgnoreQueryFilters()
                .Where(t => transactionIds.Contains(t.Id))
                .ExecuteDeleteAsync(cancellationToken);

            // 14. Delete Transfers
            result.TransfersDeleted = await _context.Transfers
                .IgnoreQueryFilters()
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 15. Delete Reconciliations
            result.ReconciliationsDeleted = await _context.Reconciliations
                .IgnoreQueryFilters()
                .Where(r => reconciliationIds.Contains(r.Id))
                .ExecuteDeleteAsync(cancellationToken);

            // 16. Delete CategorizationRules
            result.RulesDeleted = await _context.CategorizationRules
                .IgnoreQueryFilters()
                .Where(r => r.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 17. Delete Categories
            result.CategoriesDeleted = await _context.Categories
                .IgnoreQueryFilters()
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 18. Delete Accounts
            result.AccountsDeleted = await _context.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 19. Delete AkahuUserCredentials
            await _context.AkahuUserCredentials
                .IgnoreQueryFilters()
                .Where(auc => auc.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 20. Delete RefreshTokens
            await _context.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 21. Delete PasswordResetTokens
            await _context.PasswordResetTokens
                .IgnoreQueryFilters()
                .Where(prt => prt.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // 22. Delete User
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
