using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.UserData.DTOs;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services.UserData;

/// <summary>
/// Service for exporting all user data for LGPD/GDPR compliance.
/// </summary>
public class UserDataExportService : IUserDataExportService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserDataExportService> _logger;

    public UserDataExportService(
        ApplicationDbContext context,
        ILogger<UserDataExportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserDataExportDto> ExportUserDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting data export for user {UserId}", userId);

        // Get user profile (bypass soft delete filter to ensure we get the user)
        var user = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for data export", userId);
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var export = new UserDataExportDto
        {
            ExportedAt = DateTime.UtcNow,
            ExportVersion = "1.0"
        };

        // User Profile
        export.Profile = new UserProfileExportDto
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Currency = user.Currency,
            TimeZone = user.TimeZone,
            Locale = user.Locale,
            RegisteredAt = user.RegisteredAt,
            LastLoginAt = user.LastLoginAt,
            EmailConfirmed = user.EmailConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled
        };

        // Accounts (include soft-deleted for complete export)
        var accounts = await _context.Accounts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        var accountIds = accounts.Select(a => a.Id).ToList();

        // Get transaction counts per account
        var transactionCounts = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => accountIds.Contains(t.AccountId))
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AccountId, x => x.Count, cancellationToken);

        // Get last sync times from bank connections
        var bankConnectionSyncTimes = await _context.BankConnections
            .IgnoreQueryFilters()
            .Where(bc => accountIds.Contains(bc.AccountId))
            .ToDictionaryAsync(bc => bc.AccountId, bc => bc.LastSyncAt, cancellationToken);

        export.Accounts = accounts.Select(a => new AccountExportDto
        {
            Id = a.Id,
            Name = a.Name,
            Type = a.Type.ToString(),
            Currency = a.Currency,
            CurrentBalance = a.CurrentBalance,
            Institution = a.Institution,
            LastFourDigits = a.LastFourDigits,
            Notes = a.Notes,
            IsActive = a.IsActive,
            CreatedAt = a.CreatedAt,
            LastSyncAt = bankConnectionSyncTimes.GetValueOrDefault(a.Id),
            TransactionCount = transactionCounts.GetValueOrDefault(a.Id, 0)
        }).ToList();

        // Transactions (include soft-deleted)
        var transactions = await _context.Transactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => accountIds.Contains(t.AccountId))
            .Include(t => t.Category)
            .ThenInclude(c => c!.ParentCategory)
            .Include(t => t.Account)
            .ToListAsync(cancellationToken);

        export.Transactions = transactions.Select(t => new TransactionExportDto
        {
            Id = t.Id,
            AccountId = t.AccountId,
            AccountName = t.Account.Name,
            TransactionDate = t.TransactionDate,
            Amount = t.Amount,
            Description = t.Description,
            UserDescription = t.UserDescription,
            CategoryName = t.Category?.ParentCategory != null
                ? t.Category.ParentCategory.Name
                : t.Category?.Name,
            SubCategoryName = t.Category?.ParentCategory != null
                ? t.Category.Name
                : null,
            Status = t.Status.ToString(),
            Source = t.Source.ToString(),
            IsReviewed = t.IsReviewed,
            TransferId = t.TransferId,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        _logger.LogInformation("Exported {Count} transactions for user {UserId}",
            export.Transactions.Count, userId);

        // Transfers
        var transfers = await _context.Transfers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .ToListAsync(cancellationToken);

        export.Transfers = transfers.Select(t => new TransferExportDto
        {
            Id = t.Id,
            SourceAccountName = t.SourceAccount.Name,
            DestinationAccountName = t.DestinationAccount.Name,
            Amount = t.Amount,
            TransferDate = t.TransferDate,
            Description = t.Description,
            CreatedAt = t.CreatedAt
        }).ToList();

        // Categories
        var categories = await _context.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Include(c => c.ParentCategory)
            .ToListAsync(cancellationToken);

        // Get transaction counts per category
        var categoryTransactionCounts = await _context.Transactions
            .IgnoreQueryFilters()
            .Where(t => t.CategoryId.HasValue && accountIds.Contains(t.AccountId))
            .GroupBy(t => t.CategoryId!.Value)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

        export.Categories = categories.Select(c => new CategoryExportDto
        {
            Id = c.Id,
            Name = c.Name,
            ParentCategoryName = c.ParentCategory?.Name,
            Icon = c.Icon,
            Color = c.Color,
            IsSystemCategory = c.IsSystemCategory,
            TransactionCount = categoryTransactionCounts.GetValueOrDefault(c.Id, 0)
        }).ToList();

        // Categorization Rules
        var rules = await _context.CategorizationRules
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Include(r => r.Category)
            .ToListAsync(cancellationToken);

        // Get application counts per rule
        var ruleApplicationCounts = await _context.Set<Domain.Entities.RuleApplication>()
            .IgnoreQueryFilters()
            .Where(ra => rules.Select(r => r.Id).Contains(ra.RuleId))
            .GroupBy(ra => ra.RuleId)
            .Select(g => new { RuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RuleId, x => x.Count, cancellationToken);

        export.CategorizationRules = rules.Select(r => new CategorizationRuleExportDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Type = r.Type.ToString(),
            Pattern = r.Pattern,
            CategoryName = r.Category?.Name,
            Priority = r.Priority,
            IsActive = r.IsActive,
            ApplicationCount = ruleApplicationCounts.GetValueOrDefault(r.Id, 0),
            CreatedAt = r.CreatedAt
        }).ToList();

        // Bank Connections (without sensitive tokens)
        var bankConnections = await _context.BankConnections
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(bc => accountIds.Contains(bc.AccountId))
            .Include(bc => bc.Account)
            .ToListAsync(cancellationToken);

        export.BankConnections = bankConnections.Select(bc => new BankConnectionExportDto
        {
            Id = bc.Id,
            AccountName = bc.Account.Name,
            ProviderId = bc.ProviderId,
            IsActive = bc.IsActive,
            LastSyncAt = bc.LastSyncAt,
            ConnectedAt = bc.CreatedAt
        }).ToList();

        // Reconciliations
        var reconciliations = await _context.Reconciliations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => accountIds.Contains(r.AccountId))
            .Include(r => r.Account)
            .Include(r => r.ReconciliationItems)
            .ToListAsync(cancellationToken);

        export.Reconciliations = reconciliations.Select(r => new ReconciliationExportDto
        {
            Id = r.Id,
            AccountName = r.Account.Name,
            ReconciliationDate = r.ReconciliationDate,
            StatementEndDate = r.StatementEndDate,
            StatementEndBalance = r.StatementEndBalance,
            CalculatedBalance = r.CalculatedBalance,
            Status = r.Status.ToString(),
            ItemCount = r.ReconciliationItems.Count,
            CreatedAt = r.CreatedAt,
            CompletedAt = r.CompletedAt
        }).ToList();

        // Audit Logs - Get reconciliation audit logs as a sample of user activity
        var reconciliationIds = reconciliations.Select(r => r.Id).ToList();
        if (reconciliationIds.Any())
        {
            var auditLogs = await _context.ReconciliationAuditLogs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(al => reconciliationIds.Contains(al.ReconciliationId))
                .OrderByDescending(al => al.Timestamp)
                .Take(1000) // Limit to prevent massive exports
                .ToListAsync(cancellationToken);

            export.AuditLogs = auditLogs.Select(al => new AuditLogExportDto
            {
                Action = al.Action.ToString(),
                Details = al.Details,
                Timestamp = al.Timestamp
            }).ToList();
        }

        // Summary
        export.Summary = new DataSummaryDto
        {
            TotalAccounts = export.Accounts.Count,
            TotalTransactions = export.Transactions.Count,
            TotalTransfers = export.Transfers.Count,
            TotalCategories = export.Categories.Count,
            TotalRules = export.CategorizationRules.Count,
            TotalReconciliations = export.Reconciliations.Count,
            OldestTransactionDate = export.Transactions.Any()
                ? export.Transactions.Min(t => t.TransactionDate)
                : null,
            NewestTransactionDate = export.Transactions.Any()
                ? export.Transactions.Max(t => t.TransactionDate)
                : null
        };

        _logger.LogInformation(
            "Data export completed for user {UserId}: {Accounts} accounts, {Transactions} transactions, {Categories} categories",
            userId, export.Summary.TotalAccounts, export.Summary.TotalTransactions, export.Summary.TotalCategories);

        return export;
    }
}
