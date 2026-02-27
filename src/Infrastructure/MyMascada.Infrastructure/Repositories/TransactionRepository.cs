using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Common.Models;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private const int DefaultDuplicateLookbackDays = 180;

    private readonly ApplicationDbContext _context;
    private readonly ITransactionQueryService _queryService;
    private readonly IAccountAccessService _accountAccess;

    public TransactionRepository(ApplicationDbContext context, ITransactionQueryService queryService, IAccountAccessService accountAccess)
    {
        _context = context;
        _queryService = queryService;
        _accountAccess = accountAccess;
    }

    public async Task<Transaction?> GetByIdAsync(int id, Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id &&
                                     accessibleIds.Contains(t.AccountId));
    }

    public async Task<IEnumerable<Transaction>> GetByAccountIdAsync(int accountId, Guid userId)
    {
        if (!await _accountAccess.CanAccessAccountAsync(userId, accountId))
            return Enumerable.Empty<Transaction>();

        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<bool> HasTransactionsAsync(int accountId, Guid userId)
    {
        if (!await _accountAccess.CanAccessAccountAsync(userId, accountId))
            return false;

        return await _context.Transactions
            .AnyAsync(t => t.AccountId == accountId && !t.IsDeleted);
    }

    public async Task<IEnumerable<Transaction>> GetByCategoryIdAsync(int categoryId, Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.CategoryId == categoryId &&
                       accessibleIds.Contains(t.AccountId))
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Transaction> transactions, int totalCount)> GetFilteredAsync(GetTransactionsQuery request)
    {
        var parameters = TransactionQueryParameters.FromGetTransactionsQuery(request);

        // Build base query using shared service (which now uses IAccountAccessService)
        var baseQuery = await _queryService.BuildTransactionQueryAsync(parameters);

        // Get total count before pagination
        var totalCount = await baseQuery.CountAsync();

        // Apply sorting and pagination
        var sortedQuery = _queryService.ApplySorting(baseQuery, request.SortBy, request.SortDirection);
        var paginatedQuery = _queryService.ApplyPagination(sortedQuery, request.Page, request.PageSize);

        var transactions = await paginatedQuery.ToListAsync();

        return (transactions, totalCount);
    }

    public async Task<TransactionSummaryDto> GetSummaryAsync(GetTransactionsQuery request)
    {
        var parameters = TransactionQueryParameters.FromGetTransactionsQuery(request);
        var query = await _queryService.BuildTransactionQueryAsync(parameters);

        // If an account ID is provided, calculate the balance for that specific account.
        if (request.AccountId.HasValue)
        {
            Account? account = null;
            if (await _accountAccess.CanAccessAccountAsync(request.UserId, request.AccountId.Value))
            {
                account = await _context.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == request.AccountId.Value);
            }

            var initialBalance = account?.CurrentBalance ?? 0m;

            // The query is already filtered by other parameters, so we just need to calculate the sum.
            var transactionsSum = await query.SumAsync(t => t.Amount);

            // The total balance is the initial balance plus the sum of all transactions matching the query.
            var totalBalance = initialBalance + transactionsSum;

            var summary = await query
                .GroupBy(t => 1)
                .Select(g => new TransactionSummaryDto
                {
                    TotalBalance = totalBalance,
                    TotalIncome = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    TotalExpenses = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount)),
                    IncomeTransactionCount = g.Count(t => t.Amount > 0),
                    ExpenseTransactionCount = g.Count(t => t.Amount < 0),
                    TransferTransactionCount = g.Count(t => t.TransferId.HasValue),
                    UnreviewedTransactionCount = g.Count(t => !t.IsReviewed)
                })
                .FirstOrDefaultAsync();

            return summary ?? new TransactionSummaryDto { TotalBalance = totalBalance };
        }
        else
        {
            // Original logic for all accounts
            var summary = await query
                .GroupBy(t => 1) // Group all transactions together
                .Select(g => new TransactionSummaryDto
                {
                    TotalBalance = g.Sum(t => t.Amount),
                    TotalIncome = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    TotalExpenses = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount)),
                    IncomeTransactionCount = g.Count(t => t.Amount > 0),
                    ExpenseTransactionCount = g.Count(t => t.Amount < 0),
                    TransferTransactionCount = g.Count(t => t.TransferId.HasValue),
                    UnreviewedTransactionCount = g.Count(t => !t.IsReviewed)
                })
                .FirstOrDefaultAsync();

            return summary ?? new TransactionSummaryDto();
        }
    }

    public async Task<Transaction> AddAsync(Transaction transaction)
    {
        await _context.Transactions.AddAsync(transaction);
        await _context.SaveChangesAsync();



        return transaction;
    }

    public async Task UpdateAsync(Transaction transaction)
    {
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Transaction transaction)
    {
        transaction.IsDeleted = true;
        transaction.DeletedAt = DateTime.UtcNow;
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByAccountIdAsync(int accountId, Guid userId)
    {
        if (!await _accountAccess.IsOwnerAsync(userId, accountId))
            return;

        var transactions = await _context.Transactions
            .Where(t => t.AccountId == accountId && !t.IsDeleted)
            .ToListAsync();

        if (transactions.Any())
        {
            var deleteTime = DateTime.UtcNow;
            foreach (var transaction in transactions)
            {
                transaction.IsDeleted = true;
                transaction.DeletedAt = deleteTime;
            }

            _context.Transactions.UpdateRange(transactions);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<decimal> GetAccountBalanceAsync(int accountId, Guid userId)
    {
        if (!await _accountAccess.CanAccessAccountAsync(userId, accountId))
            return 0;

        // Get the account's initial balance
        var account = await _context.Accounts
            .Where(a => a.Id == accountId && !a.IsDeleted)
            .FirstOrDefaultAsync();

        if (account == null)
            return 0;

        // Get transaction balance
        var transactionBalance = await _context.Transactions
            .Where(t => t.AccountId == accountId &&
                       t.Status != TransactionStatus.Cancelled &&
                       !t.IsDeleted)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        // Return initial balance + transaction balance
        return account.CurrentBalance + transactionBalance;
    }

    public async Task<IEnumerable<Transaction>> GetRecentTransactionsAsync(Guid userId, int count = 10, CancellationToken cancellationToken = default)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => accessibleIds.Contains(t.AccountId) && !t.Account.IsDeleted)
            .OrderByDescending(t => t.TransactionDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByExternalIdAsync(string externalId, int accountId)
    {
        return await _context.Transactions
            .AnyAsync(t => t.ExternalId == externalId && t.AccountId == accountId);
    }

    public async Task<Transaction?> GetByExternalIdAsync(string externalId)
    {
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.ExternalId == externalId);
    }

    public async Task<Transaction?> GetPotentialDuplicateAsync(int accountId, decimal amount, string description, DateTime startWindow, DateTime endWindow)
    {
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t =>
                t.AccountId == accountId &&
                t.Amount == amount &&
                t.Description.Trim() == description &&
                t.TransactionDate >= startWindow &&
                t.TransactionDate <= endWindow);
    }

    public async Task<Transaction?> GetRecentDuplicateAsync(int accountId, decimal amount, string description, TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);

        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t =>
                t.AccountId == accountId &&
                t.Amount == amount &&
                t.Description.Trim() == description &&
                t.CreatedAt >= cutoffTime);
    }

    public async Task<IEnumerable<Transaction>> GetRecentAsync(Guid userId, int count = 5)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => accessibleIds.Contains(t.AccountId) && !t.Account.IsDeleted)
            .OrderByDescending(t => t.TransactionDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetByDateRangeAsync(Guid userId, int accountId, DateTime startDate, DateTime endDate, bool excludeReconciled = false)
    {
        if (!await _accountAccess.CanAccessAccountAsync(userId, accountId))
            return Enumerable.Empty<Transaction>();

        var inclusiveEndDate = endDate.EndOfDayUtc();

        var query = _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.AccountId == accountId &&
                       !t.Account.IsDeleted &&
                       t.TransactionDate >= startDate &&
                       t.TransactionDate <= inclusiveEndDate);

        // Optionally exclude already-reconciled transactions
        if (excludeReconciled)
        {
            query = query.Where(t => t.Status != TransactionStatus.Reconciled);
        }

        return await query
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        var inclusiveEndDate = endDate.EndOfDayUtc();

        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => accessibleIds.Contains(t.AccountId) &&
                       !t.Account.IsDeleted &&
                       t.TransactionDate >= startDate &&
                       t.TransactionDate <= inclusiveEndDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByDateRangeAsync(int accountId, DateTime startDate, DateTime endDate, Guid userId)
    {
        if (!await _accountAccess.CanAccessAccountAsync(userId, accountId))
            return Enumerable.Empty<Transaction>();

        var inclusiveEndDate = endDate.EndOfDayUtc();

        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.AccountId == accountId &&
                       !t.Account.IsDeleted &&
                       t.TransactionDate >= startDate &&
                       t.TransactionDate <= inclusiveEndDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<int> GetCountByUserIdAsync(Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Where(t => accessibleIds.Contains(t.AccountId) && !t.Account.IsDeleted)
            .CountAsync();
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Transaction>> GetUnreviewedAsync(Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => accessibleIds.Contains(t.AccountId) && !t.Account.IsDeleted && !t.IsReviewed && !t.IsDeleted)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<Dictionary<int, decimal>> GetAccountBalancesAsync(Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);

        // Get transaction balances
        var transactionBalances = await _context.Transactions
            .Where(t => accessibleIds.Contains(t.AccountId) && !t.Account.IsDeleted &&
                       t.Status != TransactionStatus.Cancelled &&
                       !t.IsDeleted)
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, Balance = g.Sum(t => t.Amount) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Balance);

        // Get all accessible accounts and their initial balances
        var accounts = await _context.Accounts
            .Where(a => accessibleIds.Contains(a.Id) && !a.IsDeleted)
            .Select(a => new { a.Id, a.CurrentBalance })
            .ToListAsync();

        // Combine initial balance with transaction balances
        var balances = new Dictionary<int, decimal>();
        foreach (var account in accounts)
        {
            var transactionBalance = transactionBalances.GetValueOrDefault(account.Id, 0);
            balances[account.Id] = account.CurrentBalance + transactionBalance;
        }

        return balances;
    }

    public async Task<Transaction?> GetTransactionByExternalIdAsync(string userId, string externalId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(Guid.Parse(userId));
        return await _context.Transactions
            .Include(t => t.Account)
            .Where(t => accessibleIds.Contains(t.AccountId) &&
                       t.ExternalId == externalId &&
                       !t.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<string>> GetUniqueDescriptionsAsync(Guid userId, string? searchTerm = null, int limit = 10)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        var query = _context.Transactions
            .Where(t => accessibleIds.Contains(t.AccountId) && !t.IsDeleted)
            .Select(t => t.Description)
            .Distinct();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(d => d.Contains(searchTerm));
        }

        return await query
            .OrderBy(d => d)
            .Take(limit)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    // Data integrity methods - owner-only operations
    public async Task<IEnumerable<Transaction>> GetOrphanedTransactionsAsync(Guid userId)
    {
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.Account.UserId == userId &&
                       t.Account.IsDeleted &&
                       !t.IsDeleted)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<int> GetOrphanedTransactionCountAsync(Guid userId)
    {
        return await _context.Transactions
            .Where(t => t.Account.UserId == userId &&
                       t.Account.IsDeleted &&
                       !t.IsDeleted)
            .CountAsync();
    }

    public async Task UpdateAccountIdAsync(int oldAccountId, int newAccountId, Guid userId)
    {
        if (!await _accountAccess.IsOwnerAsync(userId, oldAccountId))
            return;

        var transactions = await _context.Transactions
            .Where(t => t.AccountId == oldAccountId &&
                       !t.IsDeleted)
            .ToListAsync();

        if (transactions.Any())
        {
            foreach (var transaction in transactions)
            {
                transaction.AccountId = newAccountId;
                transaction.UpdatedAt = DateTime.UtcNow;
            }

            _context.Transactions.UpdateRange(transactions);
            await _context.SaveChangesAsync();
        }
    }

    public async Task HardDeleteTransactionsByAccountIdAsync(int accountId, Guid userId)
    {
        if (!await _accountAccess.IsOwnerAsync(userId, accountId))
            return;

        var transactions = await _context.Transactions
            .Where(t => t.AccountId == accountId)
            .ToListAsync();

        if (transactions.Any())
        {
            _context.Transactions.RemoveRange(transactions);
            await _context.SaveChangesAsync();
        }
    }

    // LLM Categorization support methods
    public async Task<IEnumerable<Transaction>> GetTransactionsByIdsAsync(IEnumerable<int> transactionIds, Guid userId, CancellationToken cancellationToken = default)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => transactionIds.Contains(t.Id) &&
                       accessibleIds.Contains(t.AccountId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .Where(c => c.UserId == userId || c.IsSystemCategory) // Include user categories and system categories
            .Where(c => !c.IsDeleted && c.IsActive) // Only active, non-deleted categories
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CategorizationRule>> GetCategorizationRulesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.CategorizationRules
            .Where(r => r.UserId == userId && r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.ConfidenceScore)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Transaction>> GetAllForDuplicateDetectionAsync(Guid userId, bool includeReviewed = false, DateTime? sinceDate = null)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        var cutoff = sinceDate ?? DateTime.UtcNow.AddDays(-DefaultDuplicateLookbackDays);

        var query = _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => accessibleIds.Contains(t.AccountId) &&
                       !t.IsDeleted &&
                       t.TransferId == null && // Exclude transfer transactions
                       t.TransactionDate >= cutoff);

        if (!includeReviewed)
        {
            query = query.Where(t => !t.IsReviewed);
        }

        return await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetUserTransactionsAsync(Guid userId, bool includeDeleted = false, bool includeReviewed = true, bool includeTransfers = true, DateTime? sinceDate = null)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        var query = _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => accessibleIds.Contains(t.AccountId));

        if (!includeDeleted)
        {
            query = query.Where(t => !t.IsDeleted);
        }

        if (!includeReviewed)
        {
            query = query.Where(t => !t.IsReviewed);
        }

        if (!includeTransfers)
        {
            query = query.Where(t => t.TransferId == null && t.Type != MyMascada.Domain.Enums.TransactionType.TransferComponent);
        }

        if (sinceDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= sinceDate.Value);
        }

        return await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.Id)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetByTransferIdAsync(Guid transferId, Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => t.TransferId == transferId && accessibleIds.Contains(t.AccountId) && !t.IsDeleted)
            .OrderBy(t => t.Id)
            .ToListAsync();
    }

    public async Task<(decimal currentMonth, decimal previousMonth)> GetMonthlySpendingAsync(int accountId, Guid userId)
    {
        if (!await _accountAccess.CanAccessAccountAsync(userId, accountId))
            return (0, 0);

        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var currentMonthEnd = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        // Get current month spending (only expenses - negative amounts)
        var currentMonthSpending = await _context.Transactions
            .Where(t => t.AccountId == accountId
                && !t.IsDeleted
                && t.Status != TransactionStatus.Cancelled
                && t.TransactionDate >= currentMonthStart
                && t.TransactionDate < currentMonthEnd
                && t.Amount < 0) // Only expenses
            .SumAsync(t => Math.Abs(t.Amount));

        // Get previous month spending (only expenses - negative amounts)
        var previousMonthSpending = await _context.Transactions
            .Where(t => t.AccountId == accountId
                && !t.IsDeleted
                && t.Status != TransactionStatus.Cancelled
                && t.TransactionDate >= previousMonthStart
                && t.TransactionDate < currentMonthStart
                && t.Amount < 0) // Only expenses
            .SumAsync(t => Math.Abs(t.Amount));

        return (currentMonthSpending, previousMonthSpending);
    }

    public async Task<IEnumerable<Transaction>> GetAllTransactionsForNormalizationAsync(Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Where(t => !t.IsDeleted && accessibleIds.Contains(t.AccountId))
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetCategorizedTransactionsAsync(Guid userId, int count = 200, CancellationToken cancellationToken = default)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Where(t => accessibleIds.Contains(t.AccountId) &&
                       t.CategoryId.HasValue &&
                       !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetUncategorizedTransactionsAsync(Guid userId, int maxCount = 500, CancellationToken cancellationToken = default)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transactions
            .Include(t => t.Account)
            .Where(t => accessibleIds.Contains(t.AccountId) &&
                       !t.CategoryId.HasValue &&
                       !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<int>> GetCategorizedTransactionIdsAsync(IEnumerable<int> transactionIds)
    {
        var ids = transactionIds.ToList();
        if (!ids.Any())
            return new HashSet<int>();

        var categorizedIds = await _context.Transactions
            .Where(t => ids.Contains(t.Id) && t.CategoryId.HasValue)
            .Select(t => t.Id)
            .ToListAsync();

        return new HashSet<int>(categorizedIds);
    }

    public async Task BulkUpdateCategorizationAsync<T>(IEnumerable<T> updates, Guid userId, CancellationToken cancellationToken = default)
        where T : class
    {
        var updateList = updates.ToList();
        if (!updateList.Any())
            return;

        // Use reflection to extract values from the anonymous objects
        var updateData = new Dictionary<int, (int CategoryId, string Method, decimal Confidence, string By, DateTime At, bool Reviewed)>();

        foreach (var update in updateList)
        {
            var properties = update.GetType().GetProperties();
            var transactionId = (int)properties.First(p => p.Name == "TransactionId").GetValue(update)!;
            var categoryId = (int)properties.First(p => p.Name == "CategoryId").GetValue(update)!;
            var method = (string)properties.First(p => p.Name == "CategorizationMethod").GetValue(update)!;
            var confidence = (decimal)properties.First(p => p.Name == "ConfidenceScore").GetValue(update)!;
            var by = (string)properties.First(p => p.Name == "AutoCategorizedBy").GetValue(update)!;
            var at = (DateTime)properties.First(p => p.Name == "AutoCategorizedAt").GetValue(update)!;
            var reviewed = (bool)properties.First(p => p.Name == "IsReviewed").GetValue(update)!;

            updateData[transactionId] = (categoryId, method, confidence, by, at, reviewed);
        }

        // Verify user owns all transactions before applying any updates
        var accessibleAccountIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        var requestedIds = updateData.Keys.ToList();
        var verifiedIds = await _context.Transactions
            .Where(t => requestedIds.Contains(t.Id) && accessibleAccountIds.Contains(t.AccountId))
            .Select(t => t.Id)
            .ToHashSetAsync(cancellationToken);

        // Drop any transaction IDs that do not belong to the user
        foreach (var id in requestedIds.Where(id => !verifiedIds.Contains(id)))
            updateData.Remove(id);

        if (updateData.Count == 0)
            return;

        // Group by categoryId to issue one batch UPDATE per category instead of one per transaction
        var byCategoryId = updateData.GroupBy(kvp => kvp.Value.CategoryId);

        foreach (var group in byCategoryId)
        {
            var categoryId = group.Key;
            var transactionIds = group.Select(g => g.Key).ToList();

            // All transactions in the same category share the same batch-level values;
            // take them from the first entry in the group.
            var (_, method, confidence, by, at, reviewed) = group.First().Value;
            var truncatedBy = by?.Length > 50 ? by.Substring(0, 50) : by;

            await _context.Transactions
                .Where(t => transactionIds.Contains(t.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.CategoryId, categoryId)
                    .SetProperty(t => t.IsAutoCategorized, true)
                    .SetProperty(t => t.AutoCategorizationMethod, method)
                    .SetProperty(t => t.AutoCategorizationConfidence, confidence)
                    .SetProperty(t => t.AutoCategorizedAt, at)
                    .SetProperty(t => t.UpdatedAt, at)
                    .SetProperty(t => t.UpdatedBy, truncatedBy)
                    .SetProperty(t => t.IsReviewed, reviewed),
                    cancellationToken);
        }
    }
}
